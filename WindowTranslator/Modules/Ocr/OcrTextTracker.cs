using System.Drawing;
using Microsoft.Extensions.Logging;
using Quickenshtein;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// OCRテキスト領域を1対1割当てと分割・統合候補によって継続追跡する。
/// </summary>
public sealed class OcrTextTracker(ILogger<OcrTextTracker> logger) : IOcrTextTracker
{
    private const int MaxMissedFrames = 3;
    private const int TextConfirmationFrames = 2;
    private const int StructureConfirmationFrames = 2;
    private const int MaxStructureMembers = 3;
    private const int MaxStructureCandidates = 6;
    private const double MinimumAssignmentScore = 0.58;

    private readonly ILogger<OcrTextTracker> logger = logger;
    private readonly object syncRoot = new();
    private readonly List<TextTrack> tracks = [];
    private Dictionary<string, int> mergeCandidates = [];
    private long nextTrackId;

    public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize)
    {
        ArgumentNullException.ThrowIfNull(observations);
        lock (this.syncRoot)
        {
            return this.UpdateCore(observations, imageSize);
        }
    }

    public void Reset()
    {
        lock (this.syncRoot)
        {
            this.tracks.Clear();
            this.mergeCandidates.Clear();
            this.nextTrackId = 0;
        }
    }

    private TextRect[] UpdateCore(IEnumerable<TextRect> observations, Size imageSize)
    {
        TextRect[] current = observations.Where(IsValid).ToArray();
        if (this.tracks.Count == 0)
        {
            foreach (TextRect observation in current)
            {
                this.CreateTrack(observation);
            }
            return this.GetOutput();
        }

        HashSet<TextTrack> matchedTracks = [];
        HashSet<int> matchedObservations = [];

        foreach ((TextTrack track, int observationIndex) in AssignOneToOne(this.tracks, current, imageSize))
        {
            track.Observe(current[observationIndex]);
            matchedTracks.Add(track);
            matchedObservations.Add(observationIndex);
        }

        this.MatchSplits(current, matchedTracks, matchedObservations);
        HashSet<TextTrack> removedTracks = this.MatchMerges(current, matchedTracks, matchedObservations);

        for (int i = 0; i < current.Length; i++)
        {
            if (!matchedObservations.Contains(i))
            {
                TextTrack track = this.CreateTrack(current[i]);
                matchedTracks.Add(track);
            }
        }

        foreach (TextTrack track in this.tracks)
        {
            if (!matchedTracks.Contains(track) && !removedTracks.Contains(track))
            {
                track.MarkMissed();
            }
        }

        foreach (TextTrack track in removedTracks)
        {
            this.tracks.Remove(track);
        }

        foreach (TextTrack track in this.tracks.Where(track => track.MissedFrames > MaxMissedFrames).ToArray())
        {
            this.logger.LogDebug("OCR track {TrackId} expired after {MissedFrames} missed frames", track.Id, track.MissedFrames);
            this.tracks.Remove(track);
        }

        return this.GetOutput();
    }

    private static bool IsValid(TextRect rect)
        => !string.IsNullOrWhiteSpace(rect.SourceText) && rect.Width > 0 && rect.Height > 0;

    private static List<(TextTrack track, int observationIndex)> AssignOneToOne(
        IReadOnlyList<TextTrack> tracks,
        TextRect[] observations,
        Size imageSize)
    {
        int count = Math.Max(tracks.Count, observations.Length);
        if (count == 0)
        {
            return [];
        }

        double[,] scores = new double[count, count];
        double[,] costs = new double[count + 1, count + 1];
        for (int trackIndex = 0; trackIndex < count; trackIndex++)
        {
            for (int observationIndex = 0; observationIndex < count; observationIndex++)
            {
                double score = trackIndex < tracks.Count && observationIndex < observations.Length
                    ? ScoreAssignment(tracks[trackIndex], observations[observationIndex], imageSize)
                    : 0;
                scores[trackIndex, observationIndex] = score;
                costs[trackIndex + 1, observationIndex + 1] = 1 - score;
            }
        }

        int[] assignment = SolveMinimumCostAssignment(costs, count);
        List<(TextTrack track, int observationIndex)> result = [];
        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            int observationIndex = assignment[trackIndex];
            if (observationIndex < observations.Length && scores[trackIndex, observationIndex] >= MinimumAssignmentScore)
            {
                result.Add((tracks[trackIndex], observationIndex));
            }
        }
        return result;
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

    private static double ScoreAssignment(TextTrack track, TextRect observation, Size imageSize)
    {
        TextRect previous = track.LatestObservation;
        double centerDistance = CenterDistance(previous, observation);
        double imageDiagonal = Math.Sqrt((double)imageSize.Width * imageSize.Width + (double)imageSize.Height * imageSize.Height);
        double distanceGate = Math.Max(imageDiagonal * 0.35, Math.Max(previous.Width, previous.Height) * 2);
        double overlap = IntersectionOverUnion(previous, observation);
        if (overlap == 0 && centerDistance > distanceGate)
        {
            return -1;
        }

        double text = TextSimilarity(track.ConfirmedText, observation.SourceText);
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

    private void MatchSplits(
        TextRect[] observations,
        HashSet<TextTrack> matchedTracks,
        HashSet<int> matchedObservations)
    {
        foreach (TextTrack track in this.tracks.Where(track => !matchedTracks.Contains(track)).ToArray())
        {
            int[] nearby = Enumerable.Range(0, observations.Length)
                .Where(index => !matchedObservations.Contains(index))
                .Where(index => IsNear(track.Stabilized, observations[index]))
                .OrderBy(index => CenterDistance(track.Stabilized, observations[index]))
                .Take(MaxStructureCandidates)
                .ToArray();

            StructureMatch<int>? best = FindBestStructureMatch(
                nearby,
                members => CombineObservations(members.Select(index => observations[index]), track.ConfirmedText),
                track.Stabilized,
                track.ConfirmedText);
            if (best is null)
            {
                continue;
            }

            track.Observe(best.Combined);
            matchedTracks.Add(track);
            foreach (int index in best.Members)
            {
                matchedObservations.Add(index);
            }
            this.logger.LogDebug("OCR track {TrackId} retained across a {PartCount}-part split", track.Id, best.Members.Count);
        }
    }

    private HashSet<TextTrack> MatchMerges(
        TextRect[] observations,
        HashSet<TextTrack> matchedTracks,
        HashSet<int> matchedObservations)
    {
        Dictionary<string, int> currentMergeCandidates = [];
        HashSet<TextTrack> removedTracks = [];

        foreach (int observationIndex in Enumerable.Range(0, observations.Length).Where(index => !matchedObservations.Contains(index)))
        {
            TextRect observation = observations[observationIndex];
            TextTrack[] nearby = this.tracks
                .Where(track => !matchedTracks.Contains(track) && !removedTracks.Contains(track))
                .Where(track => IsNear(observation, track.Stabilized))
                .OrderBy(track => CenterDistance(observation, track.Stabilized))
                .Take(MaxStructureCandidates)
                .ToArray();

            StructureMatch<TextTrack>? best = FindBestStructureMatch(
                nearby,
                members => CombineTracks(members, observation.SourceText),
                observation,
                observation.SourceText);
            if (best is null)
            {
                continue;
            }

            string key = string.Join(',', best.Members.Select(track => track.Id).Order());
            int count = this.mergeCandidates.GetValueOrDefault(key) + 1;
            currentMergeCandidates[key] = count;
            matchedObservations.Add(observationIndex);

            if (count >= StructureConfirmationFrames)
            {
                TextTrack representative = best.Members.OrderBy(track => track.Id).First();
                representative.ConfirmStructure(observation);
                matchedTracks.Add(representative);
                foreach (TextTrack track in best.Members.Where(track => track != representative))
                {
                    removedTracks.Add(track);
                    matchedTracks.Add(track);
                }
                this.logger.LogDebug("OCR tracks {TrackIds} converged into track {TrackId}", key, representative.Id);
            }
            else
            {
                foreach (TextTrack track in best.Members)
                {
                    track.MarkObservedWithoutChangingStructure();
                    matchedTracks.Add(track);
                }
                this.logger.LogDebug("OCR tracks {TrackIds} retained during a temporary merge", key);
            }
        }

        this.mergeCandidates = currentMergeCandidates;
        return removedTracks;
    }

    private static StructureMatch<T>? FindBestStructureMatch<T>(
        IReadOnlyList<T> candidates,
        Func<IReadOnlyList<T>, TextRect> combine,
        TextRect targetRect,
        string targetText)
    {
        StructureMatch<T>? best = null;
        int maxMembers = Math.Min(MaxStructureMembers, candidates.Count);
        for (int memberCount = 2; memberCount <= maxMembers; memberCount++)
        {
            foreach (IReadOnlyList<T> members in Combinations(candidates, memberCount))
            {
                TextRect combined = combine(members);
                if (!MembersAreAdjacent(members.Select(member => combine([member])).ToArray()))
                {
                    continue;
                }

                double geometry = IntersectionOverUnion(combined, targetRect);
                double text = TextSimilarity(combined.SourceText, targetText);
                double score = (geometry * 0.55) + (text * 0.45);
                if (geometry >= 0.45 && text >= 0.65 && (best is null || score > best.Score))
                {
                    best = new(members, combined, score);
                }
            }
        }
        return best;
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

    private TextTrack CreateTrack(TextRect observation)
    {
        TextTrack track = new(++this.nextTrackId, observation);
        this.tracks.Add(track);
        this.logger.LogDebug("OCR track {TrackId} created for {SourceText}", track.Id, observation.SourceText);
        return track;
    }

    private TextRect[] GetOutput()
        => this.tracks.OrderBy(track => track.Id).Select(track => track.Stabilized).ToArray();

    private sealed record StructureMatch<T>(IReadOnlyList<T> Members, TextRect Combined, double Score);

    private sealed class TextTrack(long id, TextRect observation)
    {
        private TextRect? geometryCandidate;
        private int geometryCandidateCount;
        private string? textCandidate;
        private int textCandidateCount;

        public long Id { get; } = id;

        public TextRect LatestObservation { get; private set; } = observation;

        public TextRect Stabilized { get; private set; } = observation;

        public string ConfirmedText { get; private set; } = observation.SourceText;

        public int MissedFrames { get; private set; }

        public void Observe(TextRect current)
        {
            this.MissedFrames = 0;
            this.LatestObservation = current;
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

        public void ConfirmStructure(TextRect current)
        {
            this.MissedFrames = 0;
            this.LatestObservation = current;
            this.ConfirmedText = current.SourceText;
            this.Stabilized = current;
            this.textCandidate = null;
            this.textCandidateCount = 0;
            this.geometryCandidate = null;
            this.geometryCandidateCount = 0;
        }

        public void MarkObservedWithoutChangingStructure()
        {
            this.MissedFrames = 0;
        }

        public void MarkMissed()
        {
            this.MissedFrames++;
        }

        private void UpdateText(string current)
        {
            if (current == this.ConfirmedText)
            {
                this.textCandidate = null;
                this.textCandidateCount = 0;
                return;
            }

            if (this.textCandidate is not null && TextSimilarity(this.textCandidate, current) >= 0.9)
            {
                this.textCandidate = current;
                this.textCandidateCount++;
            }
            else
            {
                this.textCandidate = current;
                this.textCandidateCount = 1;
            }

            if (this.textCandidateCount >= TextConfirmationFrames)
            {
                this.ConfirmedText = current;
                this.textCandidate = null;
                this.textCandidateCount = 0;
            }
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
    }
}
