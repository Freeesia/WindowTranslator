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
    private const int MicroGeometryConfirmationFrames = 4;
    private const int MaxStructureCandidates = 6;
    private const int MaxStructureSelectionStates = 1024;
    private const int MaxOneToOneCandidatesPerResource = 3;
    private const double MinimumAssignmentScore = 0.58;
    private const double StructureAssignmentBonus = 0.05;
    private const double TextVoteDecay = 0.75;
    private const double TextVoteThreshold = 1.5;
    private const int TextVoteHistorySize = 5;
    private const double MinimumStructureSizeRatio = 0.65;
    private const double MinimumStructureOverlap = 0.45;
    private const double MinimumStructureTextSimilarity = 0.65;
    private const double MinimumMovingStructureTextSimilarity = 0.9;
    private const double MaximumStructureAngleDifference = 8;
    private const double StrongOneToOneScore = 0.9;
    private const double AngleVectorEpsilon = 0.000000000001;
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
        List<MatchCandidate> oneToOneCandidates = BuildOneToOneCandidates(
            activeTracks,
            current,
            imageSize,
            timestamp);
        MatchCandidate[] strongMatches = SelectOneToOne(
            oneToOneCandidates.Where(IsStrongOneToOneCandidate).ToArray());
        HashSet<TextTrack> reservedTracks = strongMatches
            .SelectMany(candidate => candidate.Tracks)
            .ToHashSet();
        HashSet<int> reservedObservations = strongMatches
            .SelectMany(candidate => candidate.ObservationIndices)
            .ToHashSet();

        TextTrack[] unresolvedTracks = activeTracks
            .Where(track => !reservedTracks.Contains(track))
            .ToArray();
        List<MatchCandidate> structureCandidates = BuildStructureCandidates(
            unresolvedTracks,
            current,
            imageSize,
            reservedObservations);
        structureCandidates.AddRange(this.BuildRestorationCandidates(
            unresolvedTracks,
            current,
            imageSize,
            timestamp,
            reservedObservations));
        List<MatchCandidate> selected = [.. strongMatches, .. SelectStructures(structureCandidates)];
        reservedTracks.UnionWith(selected.SelectMany(candidate => candidate.Tracks));
        reservedObservations.UnionWith(selected.SelectMany(candidate => candidate.ObservationIndices));

        selected.AddRange(SelectOneToOne(
            oneToOneCandidates
                .Where(candidate => !reservedTracks.Contains(candidate.Tracks[0]))
                .Where(candidate => !reservedObservations.Contains(candidate.ObservationIndices[0]))
                .ToArray()));
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
        => !string.IsNullOrWhiteSpace(rect.SourceText)
            && double.IsFinite(rect.X)
            && double.IsFinite(rect.Y)
            && double.IsFinite(rect.Width)
            && double.IsFinite(rect.Height)
            && double.IsFinite(rect.FontSize)
            && double.IsFinite(rect.Angle)
            && rect.Width > 0
            && rect.Height > 0;

    private static List<MatchCandidate> BuildStructureCandidates(
        TextTrack[] tracks,
        TextRect[] observations,
        Size imageSize,
        HashSet<int> excludedObservations)
    {
        List<MatchCandidate> result = [];
        foreach (TextTrack track in tracks)
        {
            int[] nearby = Enumerable.Range(0, observations.Length)
                .Where(index => !excludedObservations.Contains(index))
                .Where(index => IsPotentialStructureMember(track.Stabilized, observations[index], imageSize))
                .OrderBy(index => CenterDistance(track.Stabilized, observations[index]))
                .Take(MaxStructureCandidates)
                .ToArray();
            for (int memberCount = 2; memberCount <= nearby.Length; memberCount++)
            {
                foreach (IReadOnlyList<int> members in Combinations(nearby, memberCount))
                {
                    TextRect[] memberRects = members.Select(index => observations[index]).ToArray();
                    if (!MembersAreAdjacent(memberRects) || !MembersHaveCompatibleStyle(memberRects))
                    {
                        continue;
                    }
                    if (TryCombineStructure(
                        memberRects,
                        track.Stabilized,
                        track.ConfirmedText,
                        imageSize,
                        out TextRect combined,
                        out double score))
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
                .Where(track => IsPotentialStructureMember(observation, track.Stabilized, imageSize))
                .OrderBy(track => CenterDistance(observation, track.Stabilized))
                .Take(MaxStructureCandidates)
                .ToArray();
            for (int memberCount = 2; memberCount <= nearby.Length; memberCount++)
            {
                foreach (IReadOnlyList<TextTrack> members in Combinations(nearby, memberCount))
                {
                    TextRect[] memberRects = members.Select(track => track.Stabilized).ToArray();
                    if (!MembersAreAdjacent(memberRects) || !MembersHaveCompatibleStyle(memberRects))
                    {
                        continue;
                    }
                    if (TryCombineStructure(
                        memberRects,
                        observation,
                        observation.SourceText,
                        imageSize,
                        out _,
                        out double score))
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
        Func<TextTrack, TextRect>? geometryOverride = null,
        Func<TextTrack, double>? distanceGateDimensionOverride = null,
        HashSet<int>? excludedObservations = null)
    {
        List<MatchCandidate> result = [];
        foreach (TextTrack track in tracks)
        {
            TextRect? geometry = geometryOverride?.Invoke(track);
            for (int observationIndex = 0; observationIndex < observations.Length; observationIndex++)
            {
                if (excludedObservations?.Contains(observationIndex) == true)
                {
                    continue;
                }
                double score = ScoreAssignment(
                    track,
                    observations[observationIndex],
                    imageSize,
                    timestamp,
                    geometry,
                    distanceGateDimensionOverride?.Invoke(track));
                if (score >= MinimumAssignmentScore)
                {
                    result.Add(new(MatchKind.OneToOne, [track], [observationIndex], observations[observationIndex], score));
                }
            }
        }
        return result;
    }

    private static bool IsStrongOneToOneCandidate(MatchCandidate candidate)
        => candidate.Score >= StrongOneToOneScore
            && NormalizeText(candidate.Tracks[0].ConfirmedText)
                == NormalizeText(candidate.Combined.SourceText);

    private static MatchCandidate[] SelectOneToOne(IReadOnlyList<MatchCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        Dictionary<TextTrack, MatchCandidate[]> candidatesByTrack = candidates
            .GroupBy(candidate => candidate.Tracks[0])
            .ToDictionary(group => group.Key, group => group.ToArray());
        Dictionary<int, MatchCandidate[]> candidatesByObservation = candidates
            .GroupBy(candidate => candidate.ObservationIndices[0])
            .ToDictionary(group => group.Key, group => group.ToArray());
        HashSet<MatchCandidate> visited = new(ReferenceEqualityComparer.Instance);
        List<MatchCandidate> selected = [];
        foreach (MatchCandidate first in candidates)
        {
            if (!visited.Add(first))
            {
                continue;
            }

            List<MatchCandidate> component = [];
            Queue<MatchCandidate> pending = new([first]);
            while (pending.TryDequeue(out MatchCandidate? candidate))
            {
                component.Add(candidate);
                foreach (MatchCandidate neighbor in candidatesByTrack[candidate.Tracks[0]])
                {
                    if (visited.Add(neighbor))
                    {
                        pending.Enqueue(neighbor);
                    }
                }
                foreach (MatchCandidate neighbor in candidatesByObservation[candidate.ObservationIndices[0]])
                {
                    if (visited.Add(neighbor))
                    {
                        pending.Enqueue(neighbor);
                    }
                }
            }
            selected.AddRange(SelectOneToOneComponent(component));
        }
        return selected.ToArray();
    }

    private static MatchCandidate[] SelectOneToOneComponent(IReadOnlyList<MatchCandidate> candidates)
    {
        if (candidates.Count == 1)
        {
            return [candidates[0]];
        }

        TextTrack[] tracks = candidates
            .Select(candidate => candidate.Tracks[0])
            .Distinct()
            .ToArray();
        int[] observations = candidates
            .Select(candidate => candidate.ObservationIndices[0])
            .Distinct()
            .Order()
            .ToArray();
        int count = Math.Max(tracks.Length, observations.Length);
        Dictionary<(TextTrack track, int observation), MatchCandidate> byPair = candidates
            .ToDictionary(candidate => (candidate.Tracks[0], candidate.ObservationIndices[0]));
        double cardinalityBonus = count + 1;
        double maximumWeight = cardinalityBonus + 1;
        double[,] costs = new double[count + 1, count + 1];
        for (int trackIndex = 0; trackIndex < count; trackIndex++)
        {
            for (int observationIndex = 0; observationIndex < count; observationIndex++)
            {
                double weight = trackIndex < tracks.Length
                    && observationIndex < observations.Length
                    && byPair.TryGetValue(
                        (tracks[trackIndex], observations[observationIndex]),
                        out MatchCandidate? candidate)
                        ? cardinalityBonus + candidate.Score
                        : 0;
                costs[trackIndex + 1, observationIndex + 1] = maximumWeight - weight;
            }
        }

        int[] assignment = SolveMinimumCostAssignment(costs, count);
        return tracks
            .Select((track, index) => assignment[index] < observations.Length
                ? byPair.GetValueOrDefault((track, observations[assignment[index]]))
                : null)
            .OfType<MatchCandidate>()
            .ToArray();
    }

    private static int[] SolveMinimumCostAssignment(double[,] costs, int count)
    {
        double[] rowPotential = new double[count + 1];
        double[] columnPotential = new double[count + 1];
        int[] matching = new int[count + 1];
        int[] path = new int[count + 1];

        for (int row = 1; row <= count; row++)
        {
            matching[0] = row;
            int column = 0;
            double[] minimum = Enumerable.Repeat(double.PositiveInfinity, count + 1).ToArray();
            bool[] used = new bool[count + 1];
            do
            {
                used[column] = true;
                int currentRow = matching[column];
                double delta = double.PositiveInfinity;
                int nextColumn = 0;
                for (int candidateColumn = 1; candidateColumn <= count; candidateColumn++)
                {
                    if (used[candidateColumn])
                    {
                        continue;
                    }
                    double reducedCost = costs[currentRow, candidateColumn]
                        - rowPotential[currentRow]
                        - columnPotential[candidateColumn];
                    if (reducedCost < minimum[candidateColumn])
                    {
                        minimum[candidateColumn] = reducedCost;
                        path[candidateColumn] = column;
                    }
                    if (minimum[candidateColumn] < delta)
                    {
                        delta = minimum[candidateColumn];
                        nextColumn = candidateColumn;
                    }
                }

                for (int candidateColumn = 0; candidateColumn <= count; candidateColumn++)
                {
                    if (used[candidateColumn])
                    {
                        rowPotential[matching[candidateColumn]] += delta;
                        columnPotential[candidateColumn] -= delta;
                    }
                    else
                    {
                        minimum[candidateColumn] -= delta;
                    }
                }
                column = nextColumn;
            }
            while (matching[column] != 0);

            do
            {
                int previousColumn = path[column];
                matching[column] = matching[previousColumn];
                column = previousColumn;
            }
            while (column != 0);
        }

        int[] result = new int[count];
        for (int column = 1; column <= count; column++)
        {
            result[matching[column] - 1] = column - 1;
        }
        return result;
    }

    private static double ScoreAssignment(
        TextTrack track,
        TextRect observation,
        Size imageSize,
        TimeSpan timestamp,
        TextRect? geometryOverride = null,
        double? distanceGateDimensionOverride = null)
    {
        TextRect previous = geometryOverride ?? track.LatestObservation;
        TextRect predicted = geometryOverride ?? track.Predict(timestamp);
        double centerDistance = CenterDistance(predicted, observation);
        double maximumDimension = Math.Max(previous.Width, previous.Height);
        double distanceGate = TrackingDistanceGate(previous, imageSize, distanceGateDimensionOverride);
        double overlap = IntersectionOverUnion(predicted, observation);
        if (overlap == 0 && centerDistance > distanceGate)
        {
            return -1;
        }

        double text = TextSimilarity(track.ConfirmedText, observation.SourceText);
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
        double contentChangeContinuity = text < 0.15 && overlap >= 0.7 && center >= 0.85
            ? 0.05
            : 0;
        return (text * 0.35)
            + (overlap * 0.25)
            + (center * 0.2)
            + (size * 0.1)
            + (aspect * 0.05)
            + (recency * 0.05)
            + contentChangeContinuity;
    }

    private static bool TryCombineStructure(
        TextRect[] source,
        TextRect targetRect,
        string targetText,
        Size imageSize,
        out TextRect combined,
        out double score)
    {
        TextRect[] members = OrderForReading(source).ToArray();
        TextRect geometry = Union(members, string.Empty);
        double overlap = IntersectionOverUnion(geometry, targetRect);
        double center = 0;
        double size = 0;
        double aspect = 0;
        double minimumTextSimilarity = MinimumStructureTextSimilarity;
        if (overlap < MinimumStructureOverlap)
        {
            double distanceGate = TrackingDistanceGate(targetRect, imageSize);
            double centerDistance = CenterDistance(geometry, targetRect);
            size = (RatioSimilarity(geometry.Width, targetRect.Width)
                + RatioSimilarity(geometry.Height, targetRect.Height)) / 2;
            aspect = RatioSimilarity(
                geometry.Width / geometry.Height,
                targetRect.Width / targetRect.Height);
            if (centerDistance > distanceGate
                || size < MinimumStructureSizeRatio
                || aspect < MinimumStructureSizeRatio)
            {
                combined = default!;
                score = -1;
                return false;
            }
            center = Math.Max(0, 1 - (centerDistance / distanceGate));
            minimumTextSimilarity = MinimumMovingStructureTextSimilarity;
        }

        if (!TrySelectCombinedText(
            members,
            targetText,
            minimumTextSimilarity,
            out string text,
            out double textSimilarity))
        {
            combined = default!;
            score = -1;
            return false;
        }

        combined = geometry with { SourceText = text };
        score = overlap >= MinimumStructureOverlap
            ? (overlap * 0.55) + (textSimilarity * 0.45) + StructureAssignmentBonus
            : (textSimilarity * 0.45)
                + (center * 0.25)
                + (size * 0.2)
                + (aspect * 0.1)
                + StructureAssignmentBonus;
        return true;
    }

    private static bool TrySelectCombinedText(
        TextRect[] members,
        string targetText,
        double minimumSimilarity,
        out string text,
        out double similarity)
    {
        int withoutSpacesLength = members.Sum(member => member.SourceText.Length);
        int withSpacesLength = withoutSpacesLength + members.Length - 1;
        string? withSpaces = null;
        string? withoutSpaces = null;
        double withSpacesSimilarity = -1;
        double withoutSpacesSimilarity = -1;
        if (CanReachTextSimilarity(withSpacesLength, targetText.Length, minimumSimilarity))
        {
            withSpaces = string.Join(' ', members.Select(member => member.SourceText));
            withSpacesSimilarity = TextSimilarity(withSpaces, targetText);
        }
        if (CanReachTextSimilarity(withoutSpacesLength, targetText.Length, minimumSimilarity))
        {
            withoutSpaces = string.Concat(members.Select(member => member.SourceText));
            withoutSpacesSimilarity = TextSimilarity(withoutSpaces, targetText);
        }
        if (withSpacesSimilarity < minimumSimilarity && withoutSpacesSimilarity < minimumSimilarity)
        {
            text = string.Empty;
            similarity = -1;
            return false;
        }

        bool useSpaces = withSpacesSimilarity >= withoutSpacesSimilarity;
        text = useSpaces ? withSpaces! : withoutSpaces!;
        similarity = useSpaces ? withSpacesSimilarity : withoutSpacesSimilarity;
        return true;
    }

    private static bool CanReachTextSimilarity(int firstLength, int secondLength, double minimumSimilarity)
    {
        int maximumLength = Math.Max(firstLength, secondLength);
        return maximumLength == 0
            || (double)Math.Min(firstLength, secondLength) / maximumLength >= minimumSimilarity;
    }

    private static List<MatchCandidate> SelectStructures(IReadOnlyList<MatchCandidate> candidates)
    {
        MatchCandidate[] ordered = candidates
            .Select(candidate => (
                Candidate: candidate,
                TrackKey: string.Join(',', candidate.Tracks.Select(track => track.Id).Order())))
            .OrderByDescending(item => item.Candidate.ResourceCount)
            .ThenByDescending(item => item.Candidate.Score)
            .ThenBy(item => item.Candidate.Kind)
            .ThenBy(item => item.Candidate.Combined.Y)
            .ThenBy(item => item.Candidate.Combined.X)
            .ThenBy(item => item.Candidate.Combined.Height)
            .ThenBy(item => item.Candidate.Combined.Width)
            .ThenBy(item => item.Candidate.Combined.Angle)
            .ThenBy(item => item.Candidate.Combined.SourceText, StringComparer.Ordinal)
            .ThenBy(item => item.TrackKey, StringComparer.Ordinal)
            .Select(item => item.Candidate)
            .ToArray();
        Dictionary<TextTrack, int> trackResources = ordered
            .SelectMany(candidate => candidate.Tracks)
            .Distinct()
            .OrderBy(track => track.Id)
            .Select((track, index) => (track, index))
            .ToDictionary(item => item.track, item => item.index);
        Dictionary<int, int> observationResources = ordered
            .SelectMany(candidate => candidate.ObservationIndices)
            .Distinct()
            .Order()
            .Select((observation, index) => (observation, index: trackResources.Count + index))
            .ToDictionary(item => item.observation, item => item.index);
        Dictionary<MatchCandidate, BigInteger> resourceMasks = new(ReferenceEqualityComparer.Instance);
        Dictionary<MatchCandidate, BigInteger> selectionOrderMasks = new(ReferenceEqualityComparer.Instance);
        for (int candidateIndex = 0; candidateIndex < ordered.Length; candidateIndex++)
        {
            MatchCandidate candidate = ordered[candidateIndex];
            BigInteger mask = BigInteger.Zero;
            foreach (TextTrack track in candidate.Tracks)
            {
                mask |= BigInteger.One << trackResources[track];
            }
            foreach (int observation in candidate.ObservationIndices)
            {
                mask |= BigInteger.One << observationResources[observation];
            }
            resourceMasks[candidate] = mask;
            selectionOrderMasks[candidate] = BigInteger.One << (ordered.Length - candidateIndex - 1);
        }

        Dictionary<BigInteger, StructureSelectionState> states = new()
        {
            [BigInteger.Zero] = new(BigInteger.Zero, BigInteger.Zero, 0, 0, null, null),
        };
        foreach (MatchCandidate candidate in ordered)
        {
            BigInteger candidateMask = resourceMasks[candidate];
            foreach (StructureSelectionState state in states.Values.ToArray())
            {
                if ((state.UsedResources & candidateMask) != BigInteger.Zero)
                {
                    continue;
                }
                BigInteger combinedMask = state.UsedResources | candidateMask;
                StructureSelectionState proposed = new(
                    combinedMask,
                    state.SelectionOrder | selectionOrderMasks[candidate],
                    state.ResourceCount + candidate.ResourceCount,
                    state.Score + candidate.Score,
                    state,
                    candidate);
                if (!states.TryGetValue(combinedMask, out StructureSelectionState? existing)
                    || IsBetterStructureSelection(proposed, existing))
                {
                    states[combinedMask] = proposed;
                }
            }

            if (states.Count > MaxStructureSelectionStates * 2)
            {
                states = states.Values
                    .OrderByDescending(state => state.ResourceCount)
                    .ThenByDescending(state => state.Score)
                    .ThenByDescending(state => state.SelectionOrder)
                    .ThenBy(state => state.UsedResources)
                    .Take(MaxStructureSelectionStates)
                    .ToDictionary(state => state.UsedResources);
            }
        }

        StructureSelectionState best = states.Values
            .OrderByDescending(state => state.ResourceCount)
            .ThenByDescending(state => state.Score)
            .ThenByDescending(state => state.SelectionOrder)
            .ThenBy(state => state.UsedResources)
            .First();
        HashSet<MatchCandidate> finalSelection = new(ReferenceEqualityComparer.Instance);
        for (StructureSelectionState? state = best; state?.Candidate is not null; state = state.Previous)
        {
            finalSelection.Add(state.Candidate);
        }
        return ordered.Where(finalSelection.Contains).ToList();
    }

    private static bool IsBetterStructureSelection(
        StructureSelectionState proposed,
        StructureSelectionState current)
        => proposed.ResourceCount > current.ResourceCount
            || (proposed.ResourceCount == current.ResourceCount
                && (proposed.Score > current.Score + 0.000001
                    || (Math.Abs(proposed.Score - current.Score) <= 0.000001
                        && proposed.SelectionOrder > current.SelectionOrder)));

    private List<MatchCandidate> BuildRestorationCandidates(
        IReadOnlyList<TextTrack> activeTracks,
        TextRect[] observations,
        Size imageSize,
        TimeSpan timestamp,
        HashSet<int> excludedObservations)
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
                child => child.RestoreGeometry(parent.LatestObservation),
                _ => Math.Max(parent.LatestObservation.Width, parent.LatestObservation.Height),
                excludedObservations);
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
                    TextRect[] assignedObservations = observationIndices
                        .Select(index => observations[index])
                        .ToArray();
                    if (!TryCombineStructure(
                        assignedObservations,
                        parent.Stabilized,
                        parent.ConfirmedText,
                        imageSize,
                        out TextRect combined,
                        out double structureScore))
                    {
                        return;
                    }
                    result.Add(new(
                        MatchKind.Restore,
                        [parent, .. children],
                        observationIndices,
                        combined,
                        Math.Max((score / children.Length) + StructureAssignmentBonus, structureScore))
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
                    child.MarkDormant(parent, timestamp);
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

    private static IEnumerable<TextRect> OrderForReading(IEnumerable<TextRect> source)
    {
        TextRect[] members = source.ToArray();
        double angle = AverageAngle(members);
        (double cosine, double sine) = ReadingAxis(angle);
        var projected = members
            .Select(rect =>
            {
                (double along, double perpendicular) = ProjectCenter(rect, cosine, sine);
                return (rect, along, perpendicular);
            })
            .ToArray();
        double perpendicularSpan = projected.Max(item => item.perpendicular)
            - projected.Min(item => item.perpendicular);
        double averageHeight = members.Average(rect => rect.Height);
        return perpendicularSpan <= averageHeight * 0.75
            ? projected.OrderBy(item => item.along).Select(item => item.rect)
            : projected.OrderBy(item => item.perpendicular).ThenBy(item => item.along).Select(item => item.rect);
    }

    private static TextRect Union(TextRect[] members, string text)
    {
        double angle = AverageAngle(members);
        (double cosine, double sine) = ReadingAxis(angle);
        (double along, double perpendicular)[] projectedCorners = members
            .SelectMany(GetCorners)
            .Select(point => ProjectPoint(point.x, point.y, cosine, sine))
            .ToArray();
        double minimumAlong = projectedCorners.Min(point => point.along);
        double maximumAlong = projectedCorners.Max(point => point.along);
        double minimumPerpendicular = projectedCorners.Min(point => point.perpendicular);
        double maximumPerpendicular = projectedCorners.Max(point => point.perpendicular);
        (double x, double y) = FromReadingCoordinates(
            minimumAlong,
            minimumPerpendicular,
            cosine,
            sine);
        double[] perpendicularPositions = members
            .Select(rect => ProjectCenter(rect, cosine, sine).perpendicular)
            .ToArray();
        double perpendicularSpan = perpendicularPositions.Max() - perpendicularPositions.Min();
        TextRect first = members[0];
        return first with
        {
            SourceText = text,
            X = x,
            Y = y,
            Width = maximumAlong - minimumAlong,
            Height = maximumPerpendicular - minimumPerpendicular,
            FontSize = members.Average(rect => rect.FontSize),
            MultiLine = perpendicularSpan > members.Average(rect => rect.Height) * 0.75
                || members.Any(rect => rect.MultiLine),
            Angle = angle,
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

    private static double NormalizeAngle(double angle)
    {
        double normalized = angle % 360;
        if (normalized > 180)
        {
            normalized -= 360;
        }
        else if (normalized <= -180)
        {
            normalized += 360;
        }
        return Math.Abs(normalized) <= AngleVectorEpsilon ? 0 : normalized;
    }

    private static bool AreAdjacent(TextRect first, TextRect second)
    {
        double angle = AverageAngle(first.Angle, second.Angle);
        (double cosine, double sine) = ReadingAxis(angle);
        (double firstCenterX, double firstCenterY) = Center(first);
        (double secondCenterX, double secondCenterY) = Center(second);
        double deltaX = secondCenterX - firstCenterX;
        double deltaY = secondCenterY - firstCenterY;
        double alongDistance = Math.Abs((deltaX * cosine) + (deltaY * sine));
        double perpendicularDistance = Math.Abs((-deltaX * sine) + (deltaY * cosine));
        double alongGap = Math.Max(0, alongDistance - ((first.Width + second.Width) / 2));
        double perpendicularGap = Math.Max(0, perpendicularDistance - ((first.Height + second.Height) / 2));
        bool sameLine = perpendicularDistance <= Math.Max(first.Height, second.Height) * 0.75
            && alongGap <= Math.Max(first.Height, second.Height) * 2;
        bool neighboringLine = alongDistance <= Math.Max(first.Width, second.Width) * 0.75
            && perpendicularGap <= Math.Max(first.Width, second.Width);
        return sameLine || neighboringLine;
    }

    private static double AverageAngle(TextRect[] members)
    {
        double sine = 0;
        double cosine = 0;
        foreach (TextRect member in members)
        {
            double radians = member.Angle * Math.PI / 180;
            sine += Math.Sin(radians);
            cosine += Math.Cos(radians);
        }
        return ResolveAverageAngle(sine, cosine, members[0].Angle);
    }

    private static double AverageAngle(double first, double second)
    {
        double firstRadians = first * Math.PI / 180;
        double secondRadians = second * Math.PI / 180;
        return ResolveAverageAngle(
            Math.Sin(firstRadians) + Math.Sin(secondRadians),
            Math.Cos(firstRadians) + Math.Cos(secondRadians),
            first);
    }

    private static double ResolveAverageAngle(double sine, double cosine, double fallback)
    {
        if (Math.Abs(sine) <= AngleVectorEpsilon && Math.Abs(cosine) <= AngleVectorEpsilon)
        {
            return fallback;
        }
        double angle = Math.Atan2(sine, cosine) * 180 / Math.PI;
        return Math.Abs(angle) <= AngleVectorEpsilon ? 0 : angle;
    }

    private static (double cosine, double sine) ReadingAxis(double angle)
    {
        double radians = angle * Math.PI / 180;
        return (Math.Cos(radians), Math.Sin(radians));
    }

    private static (double along, double perpendicular) ProjectCenter(TextRect rect, double cosine, double sine)
    {
        (double centerX, double centerY) = Center(rect);
        return ProjectPoint(centerX, centerY, cosine, sine);
    }

    private static (double along, double perpendicular) ProjectPoint(
        double x,
        double y,
        double cosine,
        double sine)
        => (
            (x * cosine) + (y * sine),
            (-x * sine) + (y * cosine));

    private static (double x, double y) FromReadingCoordinates(
        double along,
        double perpendicular,
        double cosine,
        double sine)
        => (
            (along * cosine) - (perpendicular * sine),
            (along * sine) + (perpendicular * cosine));

    private static IEnumerable<(double x, double y)> GetCorners(TextRect rect)
    {
        if (Math.Abs(rect.Angle) <= AngleVectorEpsilon)
        {
            return
            [
                (rect.X, rect.Y),
                (rect.X + rect.Width, rect.Y),
                (rect.X, rect.Y + rect.Height),
                (rect.X + rect.Width, rect.Y + rect.Height),
            ];
        }
        (double cosine, double sine) = ReadingAxis(rect.Angle);
        double alongX = rect.Width * cosine;
        double alongY = rect.Width * sine;
        double perpendicularX = -rect.Height * sine;
        double perpendicularY = rect.Height * cosine;
        return
        [
            (rect.X, rect.Y),
            (rect.X + alongX, rect.Y + alongY),
            (rect.X + perpendicularX, rect.Y + perpendicularY),
            (rect.X + alongX + perpendicularX, rect.Y + alongY + perpendicularY),
        ];
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

    private static bool IsPotentialStructureMember(TextRect target, TextRect member, Size imageSize)
        => IsNear(target, member)
            || CenterDistance(target, member) <= TrackingDistanceGate(target, imageSize);

    private static double TrackingDistanceGate(
        TextRect geometry,
        Size imageSize,
        double? maximumDimensionOverride = null)
    {
        double imageDiagonal = Math.Sqrt(
            ((double)imageSize.Width * imageSize.Width)
            + ((double)imageSize.Height * imageSize.Height));
        double maximumDimension = maximumDimensionOverride
            ?? Math.Max(geometry.Width, geometry.Height);
        return Math.Max(imageDiagonal * 0.08, maximumDimension * 4);
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
        if (Math.Abs(first.Angle) <= AngleVectorEpsilon
            && Math.Abs(second.Angle) <= AngleVectorEpsilon)
        {
            double axisLeft = Math.Max(first.X, second.X);
            double axisTop = Math.Max(first.Y, second.Y);
            double axisRight = Math.Min(first.X + first.Width, second.X + second.Width);
            double axisBottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
            double axisIntersection = Math.Max(0, axisRight - axisLeft) * Math.Max(0, axisBottom - axisTop);
            double axisUnion = (first.Width * first.Height) + (second.Width * second.Height) - axisIntersection;
            return axisUnion <= 0 ? 0 : axisIntersection / axisUnion;
        }
        RectInfo firstBounds = first.GetRotatedBoundingBox();
        RectInfo secondBounds = second.GetRotatedBoundingBox();
        double left = Math.Max(firstBounds.X, secondBounds.X);
        double top = Math.Max(firstBounds.Y, secondBounds.Y);
        double right = Math.Min(firstBounds.X + firstBounds.Width, secondBounds.X + secondBounds.Width);
        double bottom = Math.Min(firstBounds.Y + firstBounds.Height, secondBounds.Y + secondBounds.Height);
        double intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        double union = (firstBounds.Width * firstBounds.Height)
            + (secondBounds.Width * secondBounds.Height)
            - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static double CenterDistance(TextRect first, TextRect second)
    {
        if (Math.Abs(first.Angle) <= AngleVectorEpsilon
            && Math.Abs(second.Angle) <= AngleVectorEpsilon)
        {
            double axisX = (first.X + (first.Width / 2)) - (second.X + (second.Width / 2));
            double axisY = (first.Y + (first.Height / 2)) - (second.Y + (second.Height / 2));
            return Math.Sqrt((axisX * axisX) + (axisY * axisY));
        }
        (double firstCenterX, double firstCenterY) = Center(first);
        (double secondCenterX, double secondCenterY) = Center(second);
        double x = firstCenterX - secondCenterX;
        double y = firstCenterY - secondCenterY;
        return Math.Sqrt((x * x) + (y * y));
    }

    private static (double x, double y) Center(TextRect rect)
    {
        if (Math.Abs(rect.Angle) <= AngleVectorEpsilon)
        {
            return (rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        }
        (double cosine, double sine) = ReadingAxis(rect.Angle);
        return (
            rect.X + (rect.Width * cosine / 2) - (rect.Height * sine / 2),
            rect.Y + (rect.Width * sine / 2) + (rect.Height * cosine / 2));
    }

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

    private sealed record StructureSelectionState(
        BigInteger UsedResources,
        BigInteger SelectionOrder,
        int ResourceCount,
        double Score,
        StructureSelectionState? Previous,
        MatchCandidate? Candidate);

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

    private sealed class TextTrack(long id, TextRect observation, TimeSpan timestamp)
    {
        private TextRect? geometryCandidate;
        private int geometryCandidateCount;
        private readonly List<TextVote> textVotes = [];
        private DormantGeometry? dormantGeometry;
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

        public TextRect RestoreGeometry(TextRect parent)
            => this.dormantGeometry?.Restore(this.Stabilized, parent) ?? this.Stabilized;

        public void MarkDormant(TextTrack parent, TimeSpan timestamp)
        {
            this.dormantGeometry = DormantGeometry.Create(this.Stabilized, parent.Stabilized);
            this.IsDormant = true;
            this.DormantParentId = parent.Id;
            this.DormantSince = timestamp;
            this.MissedFrames = 0;
            this.ResetMotion(timestamp);
        }

        public void Reactivate(TimeSpan timestamp)
        {
            this.IsDormant = false;
            this.DormantParentId = null;
            this.dormantGeometry = null;
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

            (double currentCenterX, double currentCenterY) = Center(current);
            (double previousCenterX, double previousCenterY) = Center(this.LatestObservation);
            double movementX = currentCenterX - previousCenterX;
            double movementY = currentCenterY - previousCenterY;
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
            bool isNoise = IsGeometryNoise(this.Stabilized, current);
            bool matchesStable = isNoise && GeometrySamplesMatch(this.Stabilized, current);
            if (matchesStable && GeometryValuesEqual(this.Stabilized, current))
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

            if (this.geometryCandidate is not null
                && (isNoise
                    ? GeometrySamplesMatch(this.geometryCandidate, current)
                    : GeometryCandidateMatches(this.geometryCandidate, current)))
            {
                this.geometryCandidate = current;
                this.geometryCandidateCount++;
            }
            else
            {
                this.geometryCandidate = current;
                this.geometryCandidateCount = 1;
            }

            int confirmationFrames = matchesStable
                ? MicroGeometryConfirmationFrames
                : StructureConfirmationFrames;
            if (this.geometryCandidateCount >= confirmationFrames)
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
                && AngleDifference(stable.Angle, current.Angle) <= 2
                && stable.MultiLine == current.MultiLine;
        }

        private static bool GeometrySamplesMatch(TextRect first, TextRect second)
            => Math.Abs(first.X - second.X) <= 1
                && Math.Abs(first.Y - second.Y) <= 1
                && Math.Abs(first.Width - second.Width) <= 1
                && Math.Abs(first.Height - second.Height) <= 1
                && Math.Abs(first.FontSize - second.FontSize) <= 0.5
                && AngleDifference(first.Angle, second.Angle) <= 2
                && first.MultiLine == second.MultiLine;

        private static bool GeometryValuesEqual(TextRect first, TextRect second)
            => first.X == second.X
                && first.Y == second.Y
                && first.Width == second.Width
                && first.Height == second.Height
                && first.FontSize == second.FontSize
                && first.Angle == second.Angle
                && first.MultiLine == second.MultiLine;

        private static bool GeometryCandidateMatches(TextRect candidate, TextRect current)
            => CenterDistance(candidate, current) <= Math.Max(3, Math.Max(candidate.Width, candidate.Height) * 0.15)
                && RatioSimilarity(candidate.Width, current.Width) >= 0.85
                && RatioSimilarity(candidate.Height, current.Height) >= 0.85
                && AngleDifference(candidate.Angle, current.Angle) <= 3
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

        private sealed record DormantGeometry(
            double AlongOffsetRatio,
            double PerpendicularOffsetRatio,
            double WidthRatio,
            double HeightRatio,
            double FontSizeRatio,
            double AngleOffset)
        {
            public static DormantGeometry Create(TextRect child, TextRect parent)
            {
                (double cosine, double sine) = ReadingAxis(parent.Angle);
                double deltaX = child.X - parent.X;
                double deltaY = child.Y - parent.Y;
                double alongOffset = (deltaX * cosine) + (deltaY * sine);
                double perpendicularOffset = (-deltaX * sine) + (deltaY * cosine);
                return new(
                    alongOffset / parent.Width,
                    perpendicularOffset / parent.Height,
                    child.Width / parent.Width,
                    child.Height / parent.Height,
                    parent.FontSize > double.Epsilon ? child.FontSize / parent.FontSize : 0,
                    NormalizeAngle(child.Angle - parent.Angle));
            }

            public TextRect Restore(TextRect child, TextRect parent)
            {
                (double cosine, double sine) = ReadingAxis(parent.Angle);
                double alongOffset = this.AlongOffsetRatio * parent.Width;
                double perpendicularOffset = this.PerpendicularOffsetRatio * parent.Height;
                return child with
                {
                    X = parent.X + (alongOffset * cosine) - (perpendicularOffset * sine),
                    Y = parent.Y + (alongOffset * sine) + (perpendicularOffset * cosine),
                    Width = this.WidthRatio * parent.Width,
                    Height = this.HeightRatio * parent.Height,
                    FontSize = this.FontSizeRatio > 0
                        ? this.FontSizeRatio * parent.FontSize
                        : child.FontSize,
                    Angle = NormalizeAngle(parent.Angle + this.AngleOffset),
                };
            }
        }

        private sealed record TextVote(string Normalized, string Original);
    }
}
