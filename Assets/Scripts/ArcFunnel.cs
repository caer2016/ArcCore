using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcCore.Enumerations;

public struct ArcFunnel
{
    public LongnoteVisualState visualState;
    public bool isRed;
    public bool isHit;

    public ArcFunnel(LongnoteVisualState visualState, bool isRed, bool isHit)
    {
        this.visualState = visualState;
        this.isRed = isRed;
        this.isHit = isHit;
    }
}
