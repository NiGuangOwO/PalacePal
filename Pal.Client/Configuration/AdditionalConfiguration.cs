﻿using ECommons;
using ECommons.Configuration;
using System.Numerics;

namespace Pal.Client.Configuration
{
    public class AdditionalConfiguration : IEzConfig
    {
        public bool DisplayExit = false;
        public bool DisplayExitOnlyActive = false;
        public bool TrapColorFilled = false;
        public bool BronzeShow = false;
        public bool BronzeFill = false;
        public Vector4 BronzeColor = 4279786209.ToVector4();
        public Vector4 TrapColor = 0xFF0000FF.ToVector4();
        public float OverlayFScale = 1.3f;
    }
}
