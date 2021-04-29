﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcCore.Mathematics;

namespace ArcCore
{
    public static class Constants
    {

        public const float InputMaxY =  5.5f;
        public const float InputMinX = -8.5f;
        public const float InputMaxX =  8.5f;
        public const float ArcYZero  =  1f;

        public const float RenderFloorRange = 150f;
        public static readonly fixedQ7 RenderFloorRangeFQ7 = (fixedQ7)RenderFloorRange; //CHECK: IS THIS THE LEN OF THE FLOOR PRECISELY?

        public const int MaxPureWindow = 25;
        public const int PureWindow    = 50;
        public const int FarWindow     = 100;
        public const int LostWindow    = 120;

        public const float LaneWidth     = 2.125f;
        public const float LaneFullwidth = LaneWidth * 2;

        public const float LaneFullwidthRecip = 1 / LaneFullwidth;

    }
}
