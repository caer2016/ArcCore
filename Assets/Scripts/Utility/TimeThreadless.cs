﻿using System;

namespace ArcCore.Utility
{
    public class TimeThreadless
    {
        public static long Ticks => DateTime.Now.Ticks;
        public static float Seconds => TicksToSec(Ticks);

        public static float TicksToSec(long ticks) => ticks / 10_000_000f;

        public static float TimeSince(float sec) => Seconds - sec;
        public static long TimeSince(long ticks) => Ticks - ticks;

        public static float TimeSince_T2S(long ticks) => TicksToSec(TimeSince(ticks));
    }
}