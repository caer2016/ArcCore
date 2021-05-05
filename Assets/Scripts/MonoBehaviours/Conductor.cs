using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;
using ArcCore.Mathematics;
using ArcCore.Utilities;

namespace ArcCore.MonoBehaviours
{
    public struct TimingEvent
    {
        public int timing;
        public fixedQ7 floorPosition;
        public float bpm;
    }

    public class Conductor : MonoBehaviour
    {
        //Simple implementation with dspTime, might switch out to a deltaTime based solution if this is too unreliable
        //Conductor is responsible for playing the audio and making sure gameplay syncs. Most systems will refer to this class to get the current timing

        public static Conductor Instance { get; private set; }
        private AudioSource audioSource;

        [SerializeField] public int offset;
        [HideInInspector] private float dspStartPlayingTime;
        [HideInInspector] public List<float> groupFloorPosition;
        private List<List<TimingEvent>> timingEventGroups;
        private List<int> groupIndexCache;
        public int receptorTime;
        public long timeOfLastMix;
        public int songLength;
        public NativeArray<fixedQ7> currentFloorPosition;

        public delegate void TimeCalculatedAction(float time);
        public event TimeCalculatedAction OnTimeCalculated;
        
        public void Awake()
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
            timeOfLastMix = TimeThreadless.Ticks;
            songLength = Mathf.RoundToInt(audioSource.clip.length*1000);
        }
        
        public void PlayMusic()
        {
            dspStartPlayingTime = (float)AudioSettings.dspTime + 1f;
            audioSource.PlayScheduled(dspStartPlayingTime);
        }

        public void Update()
        {
            receptorTime = Mathf.RoundToInt(
                (float)(AudioSettings.dspTime - dspStartPlayingTime + TimeThreadless.TimeSince_T2S(timeOfLastMix)) * 1000f)
                - offset;
            UpdateCurrentFloorPosition();
            OnTimeCalculated(receptorTime);
        }
        public void SetOffset(int value)
        {
            offset = value; 
        }
        public void OnAudioFilterRead(float[] data, int channels)
        {
            timeOfLastMix = TimeThreadless.Ticks;
        }
        public void OnDestroy()
        {
            currentFloorPosition.Dispose();
        }
        public void SetupTiming(List<List<AffTiming>> timingGroups) {
            //precalculate floorposition value for timing events
            timingEventGroups = new List<List<TimingEvent>>(timingGroups.Count); 

            currentFloorPosition = new NativeArray<fixedQ7>(new fixedQ7[timingGroups.Count], Allocator.Persistent);

            for (int i=0; i<timingGroups.Count; i++)
            {
                SetupTimingGroup(timingGroups, i);
            }
            groupIndexCache = new List<int>(new int[timingEventGroups.Count]);

        }
        private void SetupTimingGroup(List<List<AffTiming>> timingGroups, int i)
        {
            timingGroups[i].Sort((item1, item2) => { return item1.timing.CompareTo(item2.timing); });

            timingEventGroups.Add(new List<TimingEvent>(timingGroups[i].Count));

            timingEventGroups[i].Add(new TimingEvent()
            {
                timing = timingGroups[i][0].timing,
                floorPosition = 0,
                bpm = timingGroups[i][0].bpm
            });

            for (int j = 1; j < timingGroups[i].Count; j++)
            {
                fixedQ7 fpos = default;

                try
                {
                    fpos = (fixedQ7)timingGroups[i][j - 1].bpm * (timingGroups[i][j].timing - timingGroups[i][j - 1].timing);
                }
                catch(ArithmeticException e)
                {
                    //DISPLAY USER ERROR LATER
                    Debug.LogWarning(e);
                    return;
                }

                Debug.Log(fpos);

                timingEventGroups[i].Add(new TimingEvent()
                {
                    timing = timingGroups[i][j].timing,
                    floorPosition = fpos,
                    bpm = timingGroups[i][j].bpm
                });
            }
        }

        public fixedQ7 GetFloorPositionFromTiming(int timing, int timingGroup)
        {
            if (timing<0) return (fixedQ7)timingEventGroups[timingGroup][0].bpm*timing / -1300;

            List<TimingEvent> group = timingEventGroups[timingGroup];
            //caching the index so we dont have to loop the entire thing every time
            //list access should be largely local anyway
            int i = groupIndexCache[timingGroup];

            while (i > 0 && group[i].timing > timing)
            {
                i--;
            }
            while (i < group.Count - 1 && group[i + 1].timing < timing)
            {
                i++;
            }

            groupIndexCache[timingGroup] = i;

            //Debug.Log((group[i].floorPosition + (timing - group[i].timing) * (fixedQ7)group[i].bpm) / -1300);
            return (group[i].floorPosition + (timing - group[i].timing) * (fixedQ7)group[i].bpm) / -1300;
        }

        public int GetTimingEventIndexFromTiming(int timing, int timingGroup)
        {
            int maxIdx = timingEventGroups[timingGroup].Count;

            for (int i = 1; i < maxIdx; i++)
            {
                if (timingEventGroups[timingGroup][i].timing > timing)
                {
                    return i - 1;
                }
            }

            return maxIdx - 1;
        }

        public TimingEvent GetTimingEventFromTiming(int timing, int timingGroup)
            => timingEventGroups[timingGroup][GetTimingEventIndexFromTiming(timing, timingGroup)];

        public TimingEvent GetTimingEvent(int timing, int timingGroup)
            => timingEventGroups[timingGroup][timing];

        public TimingEvent? GetNextTimingEventOrNull(int index, int timingGroup)
            => index + 1 >= timingEventGroups[timingGroup].Count ? (TimingEvent?)null : timingEventGroups[timingGroup][index + 1];

        public int TimingEventListLength(int timingGroup)
            => timingEventGroups[timingGroup].Count;
        public int GetFirstTimingFromFloorPosition(fixedQ7 floorposition, int timingGroup)
        {
            int maxIndex = timingEventGroups[timingGroup].Count;
            floorposition *= -1300;

            for (int i = 0; i < maxIndex - 1; i++)
            {
                TimingEvent curr = timingEventGroups[timingGroup][i];
                TimingEvent next = timingEventGroups[timingGroup][i+1];

                if ((curr.floorPosition < floorposition && next.floorPosition > floorposition)
                ||  (curr.floorPosition > floorposition && next.floorPosition < floorposition))
                {
                    float result = (float)(floorposition - curr.floorPosition) / curr.bpm + curr.timing;
                    return Mathf.RoundToInt(result);
                }
            }

            TimingEvent last = timingEventGroups[timingGroup][maxIndex-1];
            float lastresult = (float)(floorposition - last.floorPosition) / last.bpm + last.timing;
            return Mathf.RoundToInt(lastresult);
        }

        public void UpdateCurrentFloorPosition()
        {
            if (timingEventGroups == null) return;
            //Might separate the output array into its own singleton class or entity
            for (int group=0; group < timingEventGroups.Count; group++)
            {
                currentFloorPosition[group] = GetFloorPositionFromTiming(receptorTime, group);
            }
        }
    }
}