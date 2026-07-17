using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Text;
using Microsoft.Extensions.Logging;
using Quickenshtein;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// OCRテキスト領域を1対1割当てと分割・統合候補によって継続追跡する。
/// </summary>
public sealed class OcrTextTracker(ILogger<OcrTextTracker> logger) : IOcrTextTracker
{
    private const int MaxMissedFrames = 3;
    private const int StructureConfirmationFrames = 2;
    private const int MaxStructureMembers = 3;
    private const int MaxStructureCandidates = 6;
    private const int MaxOneToOneCandidatesPerResource = 3;
    private const int MaxExactAssignmentResources = 18;
    private const double MinimumAssignmentScore = 0.58;
    private const double StructureAssignmentBonus = 0.05;
    private const double TextVoteDecay = 0.75;
    private const double TextVoteThreshold = 1.5;
    private const int TextVoteHistorySize = 5;
    private const double MinimumStructureSizeRatio = 0.65;
    private const double MaximumStructureAngleDifference = 8;
    private static readonly TimeSpan dormantRetention = TimeSpan.FromSeconds(5);

    private readonly ILogger<OcrTextTracker> logger = logger;
    private readonly object syncRoot = new();
    private readonly List<TextTrack> tracks = [];
    private Dictionary<string, int> mergeCandidates = [];
    private Dictionary<long, int> dormantRestoreCandidates = [];
    private long nextTrackId;

    public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize)
        => this.Update(
            observations,
            imageSize,
            TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency));

    public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize, TimeSpan timestamp)
    {
        ArgumentNullException.ThrowIfNull(observations);
        lock (this.syncRoot)
        {
            return this.UpdateCore(observations, imageSize, timestamp);
        }
    }

    public void Reset()
    {
        lock (this.syncRoot)
        {
            this.tracks.Clear();
            this.mergeCandidates.Clear();
            this.dormantRestoreCandidates.Clear();
            this.nextTrackId = 0;
        }
    }

    private TextRect[] UpdateCore(IEnumerable<TextRect> observations, Size imageSize, TimeSpan timestamp)
    {
        TextRect[] current = observations.Where(IsValid).ToArray();
        if (this.tracks.Count == 0)
        {
            foreach (TextRect observation in current)
            {
                this.CreateTrack(observation, timestamp);
            }
            return this.GetOutput();
        }

        HashSet<TextTrack> matchedTracks = [];
        HashSet<int> matchedObservations = [];
        TextTrack[] activeTracks = this.tracks
            .Where(track => !track.IsDormant)
            .ToArray();
        List<MatchCandidate> candidates = BuildCandidates(
            activeTracks,
            current,
            imageSize,
            timestamp,
            matchedObservations);
        candidates.AddRange(this.BuildRestorationCandidates(activeTracks, current, imageSize, timestamp));
        IReadOnlyList<MatchCandidate> selected = SelectGlobally(candidates, this.tracks, current.Length);
        this.ApplyMatches(selected, timestamp, matchedTracks, matchedObservations);

        for (int i = 0; i < current.Length; i++)
        {
            if (!matchedObservations.Contains(i))
            {
                TextTrack track = this.CreateTrack(current[i], timestamp);
                matchedTracks.Add(track);
            }
        }

        foreach (TextTrack track in this.tracks.Where(track => !track.IsDormant).ToArray())
        {
            if (!matchedTracks.Contains(track))
            {
                track.MarkMissed();
            }
        }

        foreach (TextTrack track in this.tracks
            .Where(track => (!track.IsDormant && track.MissedFrames > MaxMissedFrames)
                || (track.IsDormant
                    && !matchedTracks.Contains(track)
                    && timestamp - track.DormantSince > dormantRetention))
            .ToArray())
        {
            this.logger.LogDebug("OCR track {TrackId} expired after {MissedFrames} missed frames", track.Id, track.MissedFrames);
            this.RemoveTrackTree(track);
        }

        return this.GetOutput();
    }

    private static bool IsValid(TextRect rect)
        => !string.IsNullOrWhiteSpace(rect.SourceText) && rect.Width > 0 && rect.Height > 0;

    private static List<MatchCandidate> BuildCandidates(
        TextTrack[] tracks,
        TextRect[] observations,
        Size imageSize,
        TimeSpan timestamp,
        HashSet<int> excludedObservations)
    {
        List<MatchCandidate> result = BuildOneToOneCandidates(
            tracks,
            observations,
            imageSize,
            timestamp,
            excludedObservations);

        foreach (TextTrack track in tracks)
        {
            int[] nearby = Enumerable.Range(0, observations.Length)
                .Where(index => !excludedObservations.Contains(index))
                .Where(index => IsNear(track.Stabilized, observations[index]))
                .OrderBy(index => CenterDistance(track.Stabilized, observations[index]))
                .Take(MaxStructureCandidates)
                .ToArray();
            int maxMembers = Math.Min(MaxStructureMembers, nearby.Length);
            for (int memberCount = 2; memberCount <= maxMembers; memberCount++)
            {
                foreach (IReadOnlyList<int> members in Combinations(nearby, memberCount))
                {
                    TextRect[] memberRects = members.Select(index => observations[index]).ToArray();
                    if (!MembersAreAdjacent(memberRects) || !MembersHaveCompatibleStyle(memberRects))
                    {
                        continue;
                    }
                    TextRect combined = CombineObservations(memberRects, track.ConfirmedText);
                    double score = ScoreStructure(combined, track.Stabilized, track.ConfirmedText);
                    if (score >= 0)
                    {
                        result.Add(new(MatchKind.Split, [track], members.ToArray(), combined, score));
                    }
                }
            }
        }

        foreach (int observationIndex in Enumerable.Range(0, observations.Length)
            .Where(index => !excludedObservations.Contains(index)))
        {
            TextRect observation = observations[observationIndex];
            TextTrack[] nearby = tracks
                .Where(track => IsNear(observation, track.Stabilized))
                .OrderBy(track => CenterDistance(observation, track.Stabilized))
                .Take(MaxStructureCandidates)
                .ToArray();
            int maxMembers = Math.Min(MaxStructureMembers, nearby.Length);
            for (int memberCount = 2; memberCount <= maxMembers; memberCount++)
            {
                foreach (IReadOnlyList<TextTrack> members in Combinations(nearby, memberCount))
                {
                    TextRect[] memberRects = members.Select(track => track.Stabilized).ToArray();
                    if (!MembersAreAdjacent(memberRects) || !MembersHaveCompatibleStyle(memberRects))
                    {
                        continue;
                    }
                    TextRect combinedTracks = CombineTracks(members, observation.SourceText);
                    double score = ScoreStructure(combinedTracks, observation, observation.SourceText);
                    if (score >= 0)
                    {
                        result.Add(new(MatchKind.Merge, members.ToArray(), [observationIndex], observation, score));
                    }
                }
            }
        }
        return result;
    }

    private static List<MatchCandidate> BuildOneToOneCandidates(
        IReadOnlyList<TextTrack> tracks,
        TextRect[] observations,
        Size imageSize,
        TimeSpan timestamp,
        HashSet<int> excludedObservations)
    {
        List<MatchCandidate> raw = [];
        foreach (TextTrack track in tracks)
        {
            for (int observationIndex = 0; observationIndex < observations.Length; observationIndex++)
            {
                if (excludedObservations.Contains(observationIndex))
                {
                    continue;
                }
                double score = ScoreAssignment(track, observations[observationIndex], imageSize, timestamp);
                if (score >= MinimumAssignmentScore)
                {
                    raw.Add(new(MatchKind.OneToOne, [track], [observationIndex], observations[observationIndex], score));
                }
            }
        }

        HashSet<MatchCandidate> retained = new(ReferenceEqualityComparer.Instance);
        foreach (IGrouping<TextTrack, MatchCandidate> group in raw.GroupBy(candidate => candidate.Tracks[0]))
        {
            retained.UnionWith(group.OrderByDescending(candidate => candidate.Score).Take(MaxOneToOneCandidatesPerResource));
        }
        foreach (IGrouping<int, MatchCandidate> group in raw.GroupBy(candidate => candidate.ObservationIndices[0]))
        {
            retained.UnionWith(group.OrderByDescending(candidate => candidate.Score).Take(MaxOneToOneCandidatesPerResource));
        }
        return retained.ToList();
    }

    private static double ScoreAssignment(TextTrack track, TextRect observation, Size imageSize, TimeSpan timestamp)
    {
        TextRect previous = track.LatestObservation;
        TextRect predicted = track.Predict(timestamp);
        double centerDistance = CenterDistance(predicted, observation);
        double imageDiagonal = Math.Sqrt((double)imageSize.Width * imageSize.Width + (double)imageSize.Height * imageSize.Height);
        double maximumDimension = Math.Max(previous.Width, previous.Height);
        double distanceGate = Math.Max(imageDiagonal * 0.08, maximumDimension * 4);
        double overlap = IntersectionOverUnion(predicted, observation);
        double text = TextSimilarity(track.ConfirmedText, observation.SourceText);
        if (overlap == 0 && centerDistance > distanceGate)
        {
            return -1;
        }
        if (overlap == 0 && text < 0.9 && centerDistance > maximumDimension * 1.25)
        {
            return -1;
        }
        if (text < 0.15 && overlap < 0.3)
        {
            return -1;
        }

        double center = Math.Max(0, 1 - (centerDistance / distanceGate));
        double size = (RatioSimilarity(previous.Width, observation.Width) + RatioSimilarity(previous.Height, observation.Height)) / 2;
        if (text < 0.65 && size < 0.8)
        {
            return -1;
        }
        double aspect = RatioSimilarity(previous.Width / previous.Height, observation.Width / observation.Height);
        double recency = Math.Max(0, 1 - ((double)track.MissedFrames / (MaxMissedFrames + 1)));
        return (text * 0.35) + (overlap * 0.25) + (center * 0.2) + (size * 0.1) + (aspect * 0.05) + (recency * 0.05);
    }

    private static double ScoreStructure(TextRect combined, TextRect targetRect, string targetText)
    {
        double geometry = IntersectionOverUnion(combined, targetRect);
        double text = TextSimilarity(combined.SourceText, targetText);
        return geometry >= 0.45 && text >= 0.65
            ? (geometry * 0.55) + (text * 0.45) + StructureAssignmentBonus
            : -1;
    }

    private static List<MatchCandidate> SelectGlobally(
        IReadOnlyList<MatchCandidate> candidates,
        IReadOnlyList<TextTrack> tracks,
        int observationCount)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        Dictionary<TextTrack, int> trackIndexes = tracks
            .Select((track, index) => (track, index))
            .ToDictionary(item => item.track, item => item.index);
        DisjointSet resources = new(tracks.Count + observationCount);
        foreach (MatchCandidate candidate in candidates)
        {
            int[] ids = GetResourceIds(candidate, trackIndexes, tracks.Count).ToArray();
            for (int i = 1; i < ids.Length; i++)
            {
                resources.Union(ids[0], ids[i]);
            }
        }

        List<MatchCandidate> result = [];
        foreach (IGrouping<int, MatchCandidate> component in candidates.GroupBy(candidate =>
            resources.Find(GetResourceIds(candidate, trackIndexes, tracks.Count).First())))
        {
            int[] componentResources = component
                .SelectMany(candidate => GetResourceIds(candidate, trackIndexes, tracks.Count))
                .Distinct()
                .ToArray();
            result.AddRange(componentResources.Length <= MaxExactAssignmentResources
                ? SelectExact(component.ToArray(), componentResources, trackIndexes, tracks.Count)
                : SelectLargeComponent(component.ToArray()));
        }
        return result;
    }

    private static IReadOnlyList<MatchCandidate> SelectExact(
        MatchCandidate[] candidates,
        int[] resources,
        IReadOnlyDictionary<TextTrack, int> trackIndexes,
        int observationOffset)
    {
        Dictionary<int, int> bits = resources.Select((resource, bit) => (resource, bit))
            .ToDictionary(item => item.resource, item => item.bit);
        (MatchCandidate candidate, ulong mask)[] entries = candidates
            .Select(candidate =>
            {
                ulong mask = 0;
                foreach (int resource in GetResourceIds(candidate, trackIndexes, observationOffset))
                {
                    mask |= 1UL << bits[resource];
                }
                return (candidate, mask);
            })
            .ToArray();
        Dictionary<ulong, CandidateSolution> memo = [];

        CandidateSolution Solve(ulong available)
        {
            if (available == 0)
            {
                return CandidateSolution.Empty;
            }
            if (memo.TryGetValue(available, out CandidateSolution? cached))
            {
                return cached;
            }

            int bit = BitOperations.TrailingZeroCount(available);
            ulong bitMask = 1UL << bit;
            CandidateSolution best = Solve(available & ~bitMask);
            foreach ((MatchCandidate candidate, ulong mask) in entries)
            {
                if ((mask & bitMask) == 0 || (mask & available) != mask)
                {
                    continue;
                }
                CandidateSolution remainder = Solve(available & ~mask);
                CandidateSolution proposed = remainder.Prepend(candidate);
                if (proposed.IsBetterThan(best))
                {
                    best = proposed;
                }
            }
            memo[available] = best;
            return best;
        }

        ulong all = (1UL << resources.Length) - 1;
        return Solve(all).Candidates;
    }

    private static List<MatchCandidate> SelectLargeComponent(MatchCandidate[] candidates)
    {
        List<MatchCandidate> selected = [];
        foreach (MatchCandidate candidate in candidates.OrderByDescending(candidate => candidate.Score))
        {
            MatchCandidate[] conflicts = selected.Where(existing => Conflicts(existing, candidate)).ToArray();
            if (conflicts.Length == 0)
            {
                selected.Add(candidate);
            }
            else if (candidate.Score > conflicts.Sum(conflict => conflict.Score) + 0.01)
            {
                foreach (MatchCandidate conflict in conflicts)
                {
                    selected.Remove(conflict);
                }
                selected.Add(candidate);
            }
        }
        return selected;
    }

    private static bool Conflicts(MatchCandidate first, MatchCandidate second)
        => first.Tracks.Intersect(second.Tracks).Any()
            || first.ObservationIndices.Intersect(second.ObservationIndices).Any();

    private static IEnumerable<int> GetResourceIds(
        MatchCandidate candidate,
        IReadOnlyDictionary<TextTrack, int> trackIndexes,
        int observationOffset)
        => candidate.Tracks.Select(track => trackIndexes[track])
            .Concat(candidate.ObservationIndices.Select(index => observationOffset + index));

    private List<MatchCandidate> BuildRestorationCandidates(
        IReadOnlyList<TextTrack> activeTracks,
        TextRect[] observations,
        Size imageSize,
        TimeSpan timestamp)
    {
        List<MatchCandidate> result = [];
        foreach (TextTrack parent in activeTracks)
        {
            TextTrack[] children = this.tracks
                .Where(track => track.IsDormant && track.DormantParentId == parent.Id)
                .OrderBy(track => track.Id)
                .ToArray();
            if (children.Length < 2)
            {
                continue;
            }

            List<MatchCandidate> childCandidates = BuildOneToOneCandidates(
                children,
                observations,
                imageSize,
                timestamp,
                []);
            Dictionary<TextTrack, MatchCandidate[]> candidatesByChild = children.ToDictionary(
                child => child,
                child => childCandidates
                    .Where(candidate => candidate.Tracks[0] == child)
                    .OrderByDescending(candidate => candidate.Score)
                    .Take(MaxOneToOneCandidatesPerResource)
                    .ToArray());
            if (candidatesByChild.Values.Any(candidates => candidates.Length == 0))
            {
                continue;
            }

            AddAssignments(0, [], [], 0);

            void AddAssignments(
                int childIndex,
                List<RestorationAssignment> assignments,
                HashSet<int> usedObservations,
                double score)
            {
                if (childIndex == children.Length)
                {
                    int[] observationIndices = assignments.Select(assignment => assignment.ObservationIndex).ToArray();
                    TextRect combined = CombineObservations(
                        observationIndices.Select(index => observations[index]),
                        parent.ConfirmedText);
                    result.Add(new(
                        MatchKind.Restore,
                        [parent, .. children],
                        observationIndices,
                        combined,
                        (score / children.Length) + StructureAssignmentBonus)
                    {
                        RestorationAssignments = assignments.ToArray(),
                    });
                    return;
                }

                TextTrack child = children[childIndex];
                foreach (MatchCandidate candidate in candidatesByChild[child])
                {
                    int observationIndex = candidate.ObservationIndices[0];
                    if (!usedObservations.Add(observationIndex))
                    {
                        continue;
                    }
                    assignments.Add(new(child, observationIndex, candidate.Combined));
                    AddAssignments(childIndex + 1, assignments, usedObservations, score + candidate.Score);
                    assignments.RemoveAt(assignments.Count - 1);
                    usedObservations.Remove(observationIndex);
                }
            }
        }
        return result;
    }

    private void ApplyMatches(
        IReadOnlyList<MatchCandidate> selected,
        TimeSpan timestamp,
        HashSet<TextTrack> matchedTracks,
        HashSet<int> matchedObservations)
    {
        Dictionary<string, int> currentMergeCandidates = [];
        Dictionary<long, int> currentRestoreCandidates = [];
        foreach (MatchCandidate candidate in selected)
        {
            foreach (int observationIndex in candidate.ObservationIndices)
            {
                matchedObservations.Add(observationIndex);
            }

            if (candidate.Kind == MatchKind.Restore)
            {
                TextTrack parent = candidate.Tracks[0];
                TextTrack[] children = candidate.Tracks.Skip(1).ToArray();
                int restoreCount = this.dormantRestoreCandidates.GetValueOrDefault(parent.Id) + 1;
                currentRestoreCandidates[parent.Id] = restoreCount;
                matchedTracks.Add(parent);
                matchedTracks.UnionWith(children);
                if (restoreCount >= StructureConfirmationFrames)
                {
                    foreach (RestorationAssignment assignment in candidate.RestorationAssignments)
                    {
                        assignment.Track.Reactivate(timestamp);
                        assignment.Track.Observe(assignment.Observation, timestamp);
                    }
                    this.tracks.Remove(parent);
                    this.logger.LogDebug("OCR track {TrackId} restored {ChildCount} dormant child tracks", parent.Id, children.Length);
                }
                else
                {
                    parent.Observe(candidate.Combined, timestamp);
                }
                continue;
            }

            if (candidate.Kind is MatchKind.OneToOne or MatchKind.Split)
            {
                TextTrack track = candidate.Tracks[0];
                track.Observe(candidate.Combined, timestamp);
                matchedTracks.Add(track);
                if (candidate.Kind == MatchKind.Split)
                {
                    this.logger.LogDebug("OCR track {TrackId} retained across a {PartCount}-part split", track.Id, candidate.ObservationIndices.Count);
                }
                continue;
            }

            string key = string.Join(',', candidate.Tracks.Select(track => track.Id).Order());
            int count = this.mergeCandidates.GetValueOrDefault(key) + 1;
            currentMergeCandidates[key] = count;
            foreach (TextTrack track in candidate.Tracks)
            {
                matchedTracks.Add(track);
            }
            if (count >= StructureConfirmationFrames)
            {
                TextTrack parent = this.CreateTrack(candidate.Combined, timestamp);
                matchedTracks.Add(parent);
                foreach (TextTrack child in candidate.Tracks)
                {
                    child.MarkDormant(parent.Id, timestamp);
                }
                this.logger.LogDebug("OCR tracks {TrackIds} converged into track {TrackId}", key, parent.Id);
            }
            else
            {
                foreach (TextTrack track in candidate.Tracks)
                {
                    track.MarkObservedWithoutChangingStructure(timestamp);
                }
                this.logger.LogDebug("OCR tracks {TrackIds} retained during a temporary merge", key);
            }
        }
        this.mergeCandidates = currentMergeCandidates;
        this.dormantRestoreCandidates = currentRestoreCandidates;
    }

    private void RemoveTrackTree(TextTrack track)
    {
        foreach (TextTrack child in this.tracks.Where(candidate => candidate.DormantParentId == track.Id).ToArray())
        {
            this.RemoveTrackTree(child);
        }
        this.tracks.Remove(track);
    }

    private static IEnumerable<IReadOnlyList<T>> Combinations<T>(IReadOnlyList<T> source, int count)
    {
        int[] indexes = Enumerable.Range(0, count).ToArray();
        while (true)
        {
            yield return indexes.Select(index => source[index]).ToArray();
            int position = count - 1;
            while (position >= 0 && indexes[position] == source.Count - count + position)
            {
                position--;
            }
            if (position < 0)
            {
                yield break;
            }
            indexes[position]++;
            for (int i = position + 1; i < count; i++)
            {
                indexes[i] = indexes[i - 1] + 1;
            }
        }
    }

    private static TextRect CombineObservations(IEnumerable<TextRect> source, string targetText)
    {
        TextRect[] members = OrderForReading(source).ToArray();
        string withSpaces = string.Join(' ', members.Select(member => member.SourceText));
        string withoutSpaces = string.Concat(members.Select(member => member.SourceText));
        string text = TextSimilarity(withSpaces, targetText) >= TextSimilarity(withoutSpaces, targetText)
            ? withSpaces
            : withoutSpaces;
        return Union(members, text);
    }

    private static TextRect CombineTracks(IEnumerable<TextTrack> source, string targetText)
        => CombineObservations(source.Select(track => track.Stabilized), targetText);

    private static IEnumerable<TextRect> OrderForReading(IEnumerable<TextRect> source)
    {
        TextRect[] members = source.ToArray();
        double verticalSpan = members.Max(rect => CenterY(rect)) - members.Min(rect => CenterY(rect));
        double averageHeight = members.Average(rect => rect.Height);
        return verticalSpan <= averageHeight * 0.75
            ? members.OrderBy(rect => rect.X)
            : members.OrderBy(rect => rect.Y).ThenBy(rect => rect.X);
    }

    private static TextRect Union(TextRect[] members, string text)
    {
        double left = members.Min(rect => rect.X);
        double top = members.Min(rect => rect.Y);
        double right = members.Max(rect => rect.X + rect.Width);
        double bottom = members.Max(rect => rect.Y + rect.Height);
        TextRect first = members[0];
        return first with
        {
            SourceText = text,
            X = left,
            Y = top,
            Width = right - left,
            Height = bottom - top,
            FontSize = members.Average(rect => rect.FontSize),
            MultiLine = members.Max(rect => CenterY(rect)) - members.Min(rect => CenterY(rect)) > members.Average(rect => rect.Height) * 0.75
                || members.Any(rect => rect.MultiLine),
            Angle = members.Average(rect => rect.Angle),
        };
    }

    private static bool MembersAreAdjacent(TextRect[] members)
    {
        HashSet<int> connected = [0];
        Queue<int> pending = new([0]);
        while (pending.TryDequeue(out int current))
        {
            for (int candidate = 0; candidate < members.Length; candidate++)
            {
                if (!connected.Contains(candidate) && AreAdjacent(members[current], members[candidate]))
                {
                    connected.Add(candidate);
                    pending.Enqueue(candidate);
                }
            }
        }
        return connected.Count == members.Length;
    }

    private static bool MembersHaveCompatibleStyle(TextRect[] members)
    {
        for (int first = 0; first < members.Length; first++)
        {
            for (int second = first + 1; second < members.Length; second++)
            {
                if (RatioSimilarity(members[first].Height, members[second].Height) < MinimumStructureSizeRatio
                    || RatioSimilarity(members[first].FontSize, members[second].FontSize) < MinimumStructureSizeRatio
                    || AngleDifference(members[first].Angle, members[second].Angle) > MaximumStructureAngleDifference)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static double AngleDifference(double first, double second)
    {
        double difference = Math.Abs(first - second) % 360;
        return Math.Min(difference, 360 - difference);
    }

    private static bool AreAdjacent(TextRect first, TextRect second)
    {
        double horizontalGap = Math.Max(0, Math.Max(first.X, second.X) - Math.Min(first.X + first.Width, second.X + second.Width));
        double verticalGap = Math.Max(0, Math.Max(first.Y, second.Y) - Math.Min(first.Y + first.Height, second.Y + second.Height));
        bool sameLine = Math.Abs(CenterY(first) - CenterY(second)) <= Math.Max(first.Height, second.Height) * 0.75
            && horizontalGap <= Math.Max(first.Height, second.Height) * 2;
        bool sameColumn = Math.Abs(CenterX(first) - CenterX(second)) <= Math.Max(first.Width, second.Width) * 0.75
            && verticalGap <= Math.Max(first.Width, second.Width);
        return sameLine || sameColumn;
    }

    private static bool IsNear(TextRect first, TextRect second)
    {
        if (IntersectionOverUnion(first, second) > 0)
        {
            return true;
        }
        double gate = Math.Max(Math.Max(first.Width, first.Height), Math.Max(second.Width, second.Height));
        return CenterDistance(first, second) <= gate * 1.5;
    }

    private static double TextSimilarity(string first, string second)
    {
        if (first == second)
        {
            return 1;
        }
        int maximumLength = Math.Max(first.Length, second.Length);
        if (maximumLength == 0)
        {
            return 1;
        }
        return 1 - ((double)Levenshtein.GetDistance(first, second, CalculationOptions.Default) / maximumLength);
    }

    private static string NormalizeText(string text)
    {
        StringBuilder result = new(text.Length);
        bool previousWasWhitespace = false;
        foreach (char character in text.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && result.Length > 0)
                {
                    result.Append(' ');
                }
                previousWasWhitespace = true;
            }
            else
            {
                result.Append(character);
                previousWasWhitespace = false;
            }
        }
        return result.ToString().TrimEnd();
    }

    private static double IntersectionOverUnion(TextRect first, TextRect second)
    {
        double left = Math.Max(first.X, second.X);
        double top = Math.Max(first.Y, second.Y);
        double right = Math.Min(first.X + first.Width, second.X + second.Width);
        double bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        double intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        double union = (first.Width * first.Height) + (second.Width * second.Height) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static double CenterDistance(TextRect first, TextRect second)
    {
        double x = CenterX(first) - CenterX(second);
        double y = CenterY(first) - CenterY(second);
        return Math.Sqrt((x * x) + (y * y));
    }

    private static double CenterX(TextRect rect) => rect.X + (rect.Width / 2);

    private static double CenterY(TextRect rect) => rect.Y + (rect.Height / 2);

    private static double RatioSimilarity(double first, double second)
    {
        double maximum = Math.Max(Math.Abs(first), Math.Abs(second));
        return maximum <= double.Epsilon ? 1 : Math.Min(Math.Abs(first), Math.Abs(second)) / maximum;
    }

    private TextTrack CreateTrack(TextRect observation, TimeSpan timestamp)
    {
        TextTrack track = new(++this.nextTrackId, observation, timestamp);
        this.tracks.Add(track);
        this.logger.LogDebug("OCR track {TrackId} created for {SourceText}", track.Id, observation.SourceText);
        return track;
    }

    private TextRect[] GetOutput()
        => this.tracks
            .Where(track => !track.IsDormant)
            .OrderBy(track => track.Id)
            .Select(track => track.Stabilized)
            .ToArray();

    private enum MatchKind
    {
        OneToOne,
        Split,
        Merge,
        Restore,
    }

    private sealed record RestorationAssignment(TextTrack Track, int ObservationIndex, TextRect Observation);

    private sealed record MatchCandidate(
        MatchKind Kind,
        IReadOnlyList<TextTrack> Tracks,
        IReadOnlyList<int> ObservationIndices,
        TextRect Combined,
        double Score)
    {
        public int ResourceCount => this.Tracks.Count + this.ObservationIndices.Count;

        public IReadOnlyList<RestorationAssignment> RestorationAssignments { get; init; } = [];
    }

    private sealed record CandidateSolution(double Score, int ResourceCount, IReadOnlyList<MatchCandidate> Candidates)
    {
        public static CandidateSolution Empty { get; } = new(0, 0, []);

        public CandidateSolution Prepend(MatchCandidate candidate)
            => new(
                this.Score + candidate.Score,
                this.ResourceCount + candidate.ResourceCount,
                [candidate, .. this.Candidates]);

        public bool IsBetterThan(CandidateSolution other)
            => this.Score > other.Score + 0.000001
                || (Math.Abs(this.Score - other.Score) <= 0.000001 && this.ResourceCount > other.ResourceCount);
    }

    private sealed class DisjointSet
    {
        private readonly int[] parent;
        private readonly byte[] rank;

        public DisjointSet(int count)
        {
            this.parent = Enumerable.Range(0, count).ToArray();
            this.rank = new byte[count];
        }

        public int Find(int item)
        {
            if (this.parent[item] != item)
            {
                this.parent[item] = this.Find(this.parent[item]);
            }
            return this.parent[item];
        }

        public void Union(int first, int second)
        {
            int firstRoot = this.Find(first);
            int secondRoot = this.Find(second);
            if (firstRoot == secondRoot)
            {
                return;
            }
            if (this.rank[firstRoot] < this.rank[secondRoot])
            {
                this.parent[firstRoot] = secondRoot;
            }
            else
            {
                this.parent[secondRoot] = firstRoot;
                if (this.rank[firstRoot] == this.rank[secondRoot])
                {
                    this.rank[firstRoot]++;
                }
            }
        }
    }

    private sealed class TextTrack(long id, TextRect observation, TimeSpan timestamp)
    {
        private TextRect? geometryCandidate;
        private int geometryCandidateCount;
        private readonly List<TextVote> textVotes = [];
        private double velocityX;
        private double velocityY;
        private int motionSamples;

        public long Id { get; } = id;

        public TextRect LatestObservation { get; private set; } = observation;

        public TextRect Stabilized { get; private set; } = observation;

        public string ConfirmedText { get; private set; } = observation.SourceText;

        public int MissedFrames { get; private set; }

        public TimeSpan LastObservationTime { get; private set; } = timestamp;

        public bool IsDormant { get; private set; }

        public long? DormantParentId { get; private set; }

        public TimeSpan DormantSince { get; private set; }

        public void Observe(TextRect current, TimeSpan timestamp)
        {
            this.MissedFrames = 0;
            this.UpdateMotion(current, timestamp);
            this.LatestObservation = current;
            this.LastObservationTime = timestamp;
            this.UpdateText(current.SourceText);
            this.UpdateGeometry(current);
            this.Stabilized = current with
            {
                SourceText = this.ConfirmedText,
                X = this.Stabilized.X,
                Y = this.Stabilized.Y,
                Width = this.Stabilized.Width,
                Height = this.Stabilized.Height,
                FontSize = this.Stabilized.FontSize,
                MultiLine = this.Stabilized.MultiLine,
                Angle = this.Stabilized.Angle,
            };
        }

        public void MarkObservedWithoutChangingStructure(TimeSpan timestamp)
        {
            this.MissedFrames = 0;
            this.LastObservationTime = timestamp;
        }

        public void MarkMissed()
        {
            this.MissedFrames++;
        }

        public TextRect Predict(TimeSpan timestamp)
        {
            double seconds = (timestamp - this.LastObservationTime).TotalSeconds;
            if (this.motionSamples == 0 || seconds <= 0 || seconds > 2)
            {
                return this.LatestObservation;
            }

            double maximumDisplacement = Math.Max(this.LatestObservation.Width, this.LatestObservation.Height) * 3;
            double x = Math.Clamp(this.velocityX * seconds, -maximumDisplacement, maximumDisplacement);
            double y = Math.Clamp(this.velocityY * seconds, -maximumDisplacement, maximumDisplacement);
            return this.LatestObservation with
            {
                X = this.LatestObservation.X + x,
                Y = this.LatestObservation.Y + y,
            };
        }

        public void MarkDormant(long parentId, TimeSpan timestamp)
        {
            this.IsDormant = true;
            this.DormantParentId = parentId;
            this.DormantSince = timestamp;
            this.MissedFrames = 0;
            this.ResetMotion(timestamp);
        }

        public void Reactivate(TimeSpan timestamp)
        {
            this.IsDormant = false;
            this.DormantParentId = null;
            this.MissedFrames = 0;
            this.ResetMotion(timestamp);
        }

        private void UpdateText(string current)
        {
            string normalizedCurrent = NormalizeText(current);
            if (normalizedCurrent == NormalizeText(this.ConfirmedText))
            {
                this.textVotes.Clear();
                return;
            }

            this.textVotes.Add(new(normalizedCurrent, current));
            if (this.textVotes.Count > TextVoteHistorySize)
            {
                this.textVotes.RemoveAt(0);
            }

            (string normalized, int count, double weight)? winner = this.textVotes
                .Select((vote, index) => (vote, age: this.textVotes.Count - index - 1))
                .GroupBy(item => item.vote.Normalized)
                .Select(group => (
                    normalized: group.Key,
                    count: group.Count(),
                    weight: group.Sum(item => Math.Pow(TextVoteDecay, item.age))))
                .OrderByDescending(candidate => candidate.weight)
                .ThenByDescending(candidate => candidate.count)
                .Cast<(string normalized, int count, double weight)?>()
                .FirstOrDefault();
            if (winner is { count: >= 2, weight: >= TextVoteThreshold })
            {
                this.ConfirmedText = this.textVotes.Last(vote => vote.Normalized == winner.Value.normalized).Original;
                this.textVotes.Clear();
            }
        }

        private void UpdateMotion(TextRect current, TimeSpan timestamp)
        {
            double seconds = (timestamp - this.LastObservationTime).TotalSeconds;
            if (seconds < 0.05 || seconds > 2)
            {
                if (seconds > 2 || seconds < 0)
                {
                    this.ResetMotion(timestamp);
                }
                return;
            }

            double movementX = CenterX(current) - CenterX(this.LatestObservation);
            double movementY = CenterY(current) - CenterY(this.LatestObservation);
            double jitterTolerance = Math.Max(1, Math.Min(this.LatestObservation.Width, this.LatestObservation.Height) * 0.05);
            if (Math.Abs(movementX) <= jitterTolerance && Math.Abs(movementY) <= jitterTolerance)
            {
                this.velocityX *= 0.5;
                this.velocityY *= 0.5;
                return;
            }

            double instantX = movementX / seconds;
            double instantY = movementY / seconds;
            double newWeight = this.motionSamples == 0 ? 1 : 0.8;
            this.velocityX = (this.velocityX * (1 - newWeight)) + (instantX * newWeight);
            this.velocityY = (this.velocityY * (1 - newWeight)) + (instantY * newWeight);
            this.motionSamples++;
        }

        private void ResetMotion(TimeSpan timestamp)
        {
            this.velocityX = 0;
            this.velocityY = 0;
            this.motionSamples = 0;
            this.LastObservationTime = timestamp;
        }

        private void UpdateGeometry(TextRect current)
        {
            if (IsGeometryNoise(this.Stabilized, current))
            {
                this.geometryCandidate = null;
                this.geometryCandidateCount = 0;
                return;
            }

            double movement = CenterDistance(this.Stabilized, current);
            if (movement > Math.Max(this.Stabilized.Width, this.Stabilized.Height) * 0.75)
            {
                this.ApplyGeometry(current);
                return;
            }

            if (this.geometryCandidate is not null && GeometryCandidateMatches(this.geometryCandidate, current))
            {
                this.geometryCandidate = current;
                this.geometryCandidateCount++;
            }
            else
            {
                this.geometryCandidate = current;
                this.geometryCandidateCount = 1;
            }

            if (this.geometryCandidateCount >= StructureConfirmationFrames)
            {
                this.ApplyGeometry(current);
            }
        }

        private static bool IsGeometryNoise(TextRect stable, TextRect current)
        {
            double xTolerance = Math.Max(3, stable.Width * 0.15);
            double yTolerance = Math.Max(3, stable.Height * 0.15);
            return Math.Abs(stable.X - current.X) <= xTolerance
                && Math.Abs(stable.Y - current.Y) <= yTolerance
                && Math.Abs(stable.Width - current.Width) <= Math.Max(3, stable.Width * 0.12)
                && Math.Abs(stable.Height - current.Height) <= Math.Max(3, stable.Height * 0.12)
                && Math.Abs(stable.FontSize - current.FontSize) <= Math.Max(1, stable.FontSize * 0.12)
                && Math.Abs(stable.Angle - current.Angle) <= 2
                && stable.MultiLine == current.MultiLine;
        }

        private static bool GeometryCandidateMatches(TextRect candidate, TextRect current)
            => CenterDistance(candidate, current) <= Math.Max(3, Math.Max(candidate.Width, candidate.Height) * 0.15)
                && RatioSimilarity(candidate.Width, current.Width) >= 0.85
                && RatioSimilarity(candidate.Height, current.Height) >= 0.85
                && Math.Abs(candidate.Angle - current.Angle) <= 3
                && candidate.MultiLine == current.MultiLine;

        private void ApplyGeometry(TextRect current)
        {
            this.Stabilized = this.Stabilized with
            {
                X = current.X,
                Y = current.Y,
                Width = current.Width,
                Height = current.Height,
                FontSize = current.FontSize,
                MultiLine = current.MultiLine,
                Angle = current.Angle,
            };
            this.geometryCandidate = null;
            this.geometryCandidateCount = 0;
        }

        private sealed record TextVote(string Normalized, string Original);
    }
}
