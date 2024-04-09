using System;
using System.Collections.Generic;
using System.Linq;

namespace MetaverseCloudEngine.Unity.OpenCV.BYTETracker
{
    /// <summary>
    /// ByteTracker:
    /// C# implementation of ByteTrack that does not include an object detection algorithm.
    /// The implementation is based on "ByteTrack-cpp". (https://github.com/derpda/ByteTrack-cpp/)
    /// Only tracking algorithm are implemented.
    /// Any object detection algorithm can be easily combined.
    /// Some code has been modified to obtain the same processing results as the original code below.
    /// https://github.com/ifzhang/ByteTrack/tree/main/deploy/ncnn/cpp
    /// https://github.com/ifzhang/ByteTrack/tree/main/yolox/tracker
    /// </summary>
    public class ByteTracker
    {
        private readonly float _trackThresh;
        private readonly float _highThresh;
        private readonly float _matchThresh;

        private readonly int _maxTimeLost;
        private int _frameID;
        private int _trackIDCount;
        private readonly bool _mot20;

        private List<Track> _trackedTracks = new();
        private List<Track> _lostTracks = new();
        private List<Track> _removedTracks = new();

        /// <summary>
        /// Creates a new instance of <see cref="ByteTracker"/>.
        /// </summary>
        /// <param name="maxRetentionTime">The maximum number of frames that an object is retained for.</param>
        /// <param name="trackThresh">tracking confidence threshold.</param>
        /// <param name="highThresh">confidence threshold for new tracking to be added. (track_thresh + 1)</param>
        /// <param name="matchThresh">matching threshold for tracking.</param>
        /// <param name="mot20">I have no clue what this does.</param>
        public ByteTracker(int maxRetentionTime, float trackThresh = 0.5f,
                            float highThresh = 0.6f, float matchThresh = 0.8f, bool mot20 = false)
        {
            _trackThresh = trackThresh;
            _highThresh = highThresh;
            _matchThresh = matchThresh;
            _maxTimeLost = maxRetentionTime;
            _frameID = 0;
            _trackIDCount = 0;
            _mot20 = mot20;
        }

        public List<Track> Update(List<Detection> inputDetections)
        {
            _frameID++;

            ////////// Step 1: Get detections
            // Sort new tracks from detection by score
            var detections = new List<Detection>();
            var lowScoreDetections = new List<Detection>();
            foreach (var detection in inputDetections)
            {
                if (detection.Score >= _trackThresh)
                    detections.Add(detection);
                else
                    lowScoreDetections.Add(detection);
            }

            // Sort existing tracks by confirmed status
            var confirmedTracks = new List<Track>();
            var unconfirmedTracks = new List<Track>();
            foreach (var track in _trackedTracks)
            {
                if (!track.IsConfirmed)
                    unconfirmedTracks.Add(track);
                else
                    confirmedTracks.Add(track);
            }

            var trackPool = JointTracks(confirmedTracks, _lostTracks);

            // Predict current pose by KF
            foreach (var track in trackPool)
                track.Predict();


            ////////// Step 2: Find matches between tracks and detections
            ////////// Step 2: First association, with IoU
            var (matchedTracks, unmatchedTrackedTracks, unmatchedDetections) = IouAssociation(trackPool, detections);

            ////////// Step 3: Second association, using low score detections
            var newLostTracks =
                LowScoreAssociation(matchedTracks, lowScoreDetections, unmatchedTrackedTracks);

            ////////// Step 4: Init new tracks
            var newRemovedTracks =
                InitNewTracks(matchedTracks, unconfirmedTracks, unmatchedDetections);

            ////////// Step 5: Update state
            foreach (var lostTrack in _lostTracks.Where(lostTrack => _frameID - lostTrack.FrameId > _maxTimeLost))
            {
                lostTrack.MarkAsRemoved();
                newRemovedTracks.Add(lostTrack);
            }

            _lostTracks = SubTracks(
                JointTracks(SubTracks(_lostTracks, matchedTracks), newLostTracks),
                _removedTracks);
            _removedTracks = JointTracks(_removedTracks, newRemovedTracks);

            (_trackedTracks, _lostTracks) =
                RemoveDuplicateTracks(matchedTracks, _lostTracks);

            var outputTracks = _trackedTracks.Where(track => track.IsConfirmed).ToList();
            outputTracks.AddRange(_lostTracks.Where(track => track.IsConfirmed));
            return outputTracks;
        }

        public void Clear()
        {
            _trackedTracks.Clear();
            _lostTracks.Clear();
            _removedTracks.Clear();
            _frameID = 0;
            _trackIDCount = 0;
        }

        private (List<Track>, List<Track>, List<Detection>) IouAssociation(
        List<Track> trackPool, List<Detection> detections)
        {
            var (matches, unmatchedTracks, unmatchedDetections) =
                LinearAssignment(trackPool, detections, _matchThresh, !_mot20);

            var matchedTracks = new List<Track>();
            foreach (var (track, detection) in matches)
            {
                track.Update(detection, _frameID);
                matchedTracks.Add(track);
            }

            var unmatchedTrackedTracks = unmatchedTracks.Where(unmatch => unmatch.DetectionState == TrackState.Tracked).ToList();
            return (matchedTracks, unmatchedTrackedTracks, unmatchedDetections);
        }

        private List<Track> LowScoreAssociation(
            ICollection<Track> matchedTracks,
            List<Detection> lowScoreDetections,
            List<Track> unmatchedTrackedTracks)
        {
            var (matches, unmatchedTracks, _) =
                LinearAssignment(unmatchedTrackedTracks, lowScoreDetections, 0.5f, false);

            foreach (var (track, detection) in matches)
            {
                track.Update(detection, _frameID);
                matchedTracks.Add(track);
            }

            var newLostTracks = new List<Track>();
            foreach (var track in unmatchedTracks.Where(track => track.DetectionState != TrackState.Lost))
            {
                track.MarkAsLost();
                newLostTracks.Add(track);
            }

            return newLostTracks;
        }

        private List<Track> InitNewTracks(
            List<Track> matchedTracks,
            List<Track> unconfirmedTracks,
            List<Detection> unmatchedDetections)
        {
            // Deal with unconfirmed tracks, usually tracks with only one beginning frame
            var (matches, unmatchedUnconfirmedTracks, newDetections) =
                LinearAssignment(unconfirmedTracks, unmatchedDetections, 0.7f, !_mot20);

            foreach (var match in matches)
            {
                match.Item1.Update(match.Item2, _frameID);
                matchedTracks.Add(match.Item1);
            }

            var newRemovedTracks = new List<Track>();
            foreach (var track in unmatchedUnconfirmedTracks)
            {
                track.MarkAsRemoved();
                newRemovedTracks.Add(track);
            }

            // Add new tracks
            foreach (var detection in newDetections.Where(detection => !(detection.Score < _highThresh)))
            {
                _trackIDCount++;
                var newTrack = new Track(detection, _frameID, _trackIDCount);
                matchedTracks.Add(newTrack);
            }

            return newRemovedTracks;
        }

        private List<Track> JointTracks(
            List<Track> aTlist,
            List<Track> bTlist)
        {
            var exists = new HashSet<int>();
            var res = new List<Track>();

            foreach (var track in aTlist)
            {
                exists.Add(track.TrackId);
                res.Add(track);
            }

            res.AddRange(bTlist.Where(track => !exists.Contains(track.TrackId)));

            return res;
        }

        private List<Track> SubTracks(
            List<Track> aTlist,
            List<Track> bTlist)
        {
            var tracks = new Dictionary<int, Track>();
            foreach (var track in aTlist)
            {
                tracks[track.TrackId] = track;
            }

            foreach (var track in bTlist)
            {
                tracks.Remove(track.TrackId);
            }

            return tracks.Select(kvp => kvp.Value).ToList();
        }

        private (List<Track>, List<Track>) RemoveDuplicateTracks(
            List<Track> aTracks,
            List<Track> bTracks)
        {
            if (aTracks.Count == 0 || bTracks.Count == 0)
            {
                return (aTracks, bTracks);
            }

            var iOus = new float[aTracks.Count, bTracks.Count];
            for (var ai = 0; ai < aTracks.Count; ai++)
            {
                for (var bi = 0; bi < bTracks.Count; bi++)
                {
                    iOus[ai, bi] = 1 - RectOperations.CalcIoU(bTracks[bi].PredictedRect, aTracks[ai].PredictedRect);
                }
            }

            var aOverlapping = new bool[aTracks.Count];
            var bOverlapping = new bool[bTracks.Count];

            for (var ai = 0; ai < iOus.GetLength(0); ai++)
            {
                for (var bi = 0; bi < iOus.GetLength(1); bi++)
                {
                    if (iOus[ai, bi] >= 0.15)
                        continue;
                    var timeP = aTracks[ai].FrameId - aTracks[ai].StartFrameId;
                    var timeQ = bTracks[bi].FrameId - bTracks[bi].StartFrameId;
                    if (timeP > timeQ)
                        bOverlapping[bi] = true;
                    else
                        aOverlapping[ai] = true;
                }
            }

            var aTracksOut = aTracks.Where((_, ai) => !aOverlapping[ai]).ToList();
            var bTracksOut = bTracks.Where((_, bi) => !bOverlapping[bi]).ToList();
            return (aTracksOut, bTracksOut);
        }

        private (
            List<(Track, Detection)>,
            List<Track>,
            List<Detection>
        ) LinearAssignment(
            List<Track> tracks,
            List<Detection> detections,
            float thresh,
            bool useFuseScore = true)
        {
            if (tracks.Count == 0 || detections.Count == 0)
            {
                return (new List<(Track, Detection)>(), tracks, detections);
            }

            var costMatrix = tracks.Select(t1 =>
                detections.Select(t => 1 - RectOperations.CalcIoU(t.Rect, t1.PredictedRect)).ToList()).ToList();
            if (useFuseScore)
            {
                costMatrix = FuseScore(costMatrix, detections);
            }

            var matches = new List<(Track, Detection)>();
            var aUnmatched = new List<Track>();
            var bUnmatched = new List<Detection>();

            var (rowSol, colSol) = ExecLapjv(costMatrix, true, thresh);
            for (var i = 0; i < rowSol.Count; i++)
            {
                if (rowSol[i] >= 0)
                    matches.Add((tracks[i], detections[rowSol[i]]));
                else
                    aUnmatched.Add(tracks[i]);
            }

            for (var i = 0; i < colSol.Count; i++)
                if (colSol[i] < 0) bUnmatched.Add(detections[i]);

            return (matches, aUnmatched, bUnmatched);
        }

        private List<List<float>> FuseScore(List<List<float>> costMatrix, List<Detection> detections)
        {
            if (costMatrix.Count == 0)
                return costMatrix;

            var nRows = costMatrix.Count;
            var nCols = costMatrix[0].Count;

            for (var i = 0; i < nRows; i++)
            {
                for (var j = 0; j < nCols; j++)
                {
                    var detScore = detections[j].Score;
                    var iouSim = 1.0f - costMatrix[i][j];
                    var fuseSim = iouSim * detScore;
                    costMatrix[i][j] = 1.0f - fuseSim;
                }
            }

            return costMatrix;
        }

        private static (List<int>, List<int>) ExecLapjv(List<List<float>> cost,
            bool extendCost = false,
            float costLimit = float.MaxValue)
        {
            var nRows = cost.Count;
            var nCols = cost[0].Count;
            var rowSol = new List<int>(new int[nRows]);
            var colSol = new List<int>(new int[nCols]);

            if (nRows != nCols && !extendCost)
                throw new InvalidOperationException("The `extendCost` variable should be set to true.");

            int n;
            List<float> costC;

            if (extendCost || costLimit < float.MaxValue)
            {
                n = nRows + nCols;
                costC = new List<float>(n * n);

                if (costLimit < float.MaxValue)
                    costC.AddRange(Enumerable.Repeat(costLimit / 2.0f, n * n));
                else
                {
                    var costMax = cost.SelectMany(row => row).Prepend(-1).Max();
                    costC.AddRange(Enumerable.Repeat(costMax + 1, n * n));
                }

                for (var i = nRows; i < n; i++)
                    for (var j = nCols; j < n; j++)
                        costC[i * n + j] = 0;

                for (var i = 0; i < nRows; i++)
                    for (var j = 0; j < nCols; j++)
                        costC[i * n + j] = cost[i][j];
            }
            else
            {
                n = nRows;
                costC = new List<float>(nRows * nCols);

                for (var i = 0; i < nRows; i++)
                    for (var j = 0; j < nCols; j++)
                        costC[i * nCols + j] = cost[i][j];
            }

            var xC = Enumerable.Repeat(0, n).ToList();
            var yC = Enumerable.Repeat(0, n).ToList();

            var ret = Lapjv.LapjvInternal(n, costC, xC, yC);
            if (ret != 0)
                throw new InvalidOperationException("The result of LapjvInternal() is invalid.");

            if (n == nRows) 
                return (rowSol, colSol);
            for (var i = 0; i < n; i++)
            {
                if (xC[i] >= nCols) xC[i] = -1;
                if (yC[i] >= nRows) yC[i] = -1;
            }
            for (var i = 0; i < nRows; i++)
                rowSol[i] = xC[i];
            for (var i = 0; i < nCols; i++)
                colSol[i] = yC[i];

            return (rowSol, colSol);
        }
    }
}
