using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public struct TransformState
    {
        public readonly uint id;
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public TransformState(Vector3 position, Quaternion rotation)
        {
            id = default;
            this.position = position;
            this.rotation = rotation;
        }
        public TransformState(uint id, Vector3 position, Quaternion rotation)
        {
            this.id = id;
            this.position = position;
            this.rotation = rotation;
        }

        public override string ToString()
        {
            return $"[{id}, {position}, {rotation}]";
        }
    }

    public class SnapshotBuffer
    {
        static readonly ILogger logger = LogFactory.GetLogger<SnapshotBuffer>(LogType.Error);
        struct Snapshot
        {
            /// <summary>
            /// Server Time
            /// </summary>
            public readonly float time;
            public readonly TransformState state;

            public Snapshot(TransformState state, float time) : this()
            {
                this.state = state;
                this.time = time;
            }
        }

        readonly List<Snapshot> buffer = new List<Snapshot>();

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count == 0;
        }

        public void AddSnapShot(TransformState state, float serverTime)
        {
            buffer.Add(new Snapshot(state, serverTime));
        }

        /// <summary>
        /// Gets snapshot to use for interoplation
        /// <para>this method should not be called when there are no snapshots in buffer</para>
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public TransformState GetLinearInterpolation(float now)
        {
            if (buffer.Count == 0)
            {
                logger.LogError("No snapshots, returning default");
                return default;
            }

            // first snapshot
            if (buffer.Count == 1)
            {
                if (logger.LogEnabled()) logger.Log("First snapshot");

                Snapshot only = buffer[0];
                return only.state;
            }

            for (int i = 0; i < buffer.Count - 1; i++)
            {
                Snapshot from = buffer[i];
                Snapshot to = buffer[i + 1];
                float fromTime = buffer[i].time;
                float toTime = buffer[i + 1].time;

                // if between times, then use from/to
                if (fromTime < now && now < toTime)
                {
                    float alpha = Mathf.Clamp01((now - fromTime) / (toTime - fromTime));

                    Vector3 pos = Vector3.Lerp(from.state.position, to.state.position, alpha);
                    Quaternion rot = Quaternion.Slerp(from.state.rotation, to.state.rotation, alpha);
                    return new TransformState(pos, rot);
                }
            }

            // if no valid snapshot use last
            // this can happen if server hasn't sent new data
            // there could be no new data from either lag or because object hasn't moved
            Snapshot last = buffer[buffer.Count - 1];
            if (logger.WarnEnabled()) logger.LogWarning($"No snapshot for t={now} using first t={buffer[0].time} last t={last.time}");
            return last.state;
        }

        /// <summary>
        /// removes snapshots older than <paramref name="oldTime"/>, but keeps atleast <paramref name="keepCount"/> snapshots in buffer
        /// </summary>
        /// <param name="oldTime"></param>
        /// <param name="keepCount">minium number of snapshots to keep in buffer</param>
        public void RemoveOldSnapshots(float oldTime, int keepCount)
        {
            for (int i = buffer.Count - 1 - keepCount; i >= 0; i--)
            {
                // older than oldTime
                if (buffer[i].time < oldTime)
                {
                    buffer.RemoveAt(i);
                }
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"count:{buffer.Count}, minTime:{buffer[0].time}, maxTime:{buffer[buffer.Count - 1].time}");
            for (int i = 0; i < buffer.Count; i++)
            {
                builder.AppendLine($"  {i}: {buffer[i].time}");
            }
            return builder.ToString();
        }
    }
}
