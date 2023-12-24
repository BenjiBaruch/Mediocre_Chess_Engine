using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class ChessAbstract : MonoBehaviour
{
    public abstract string Name { get; set; }
    public abstract string Version { get; set; }
    public bool Side { get; set; }
    public abstract void Initialize(bool side);
    public abstract Move GetMoveTimed(BoardStruct b, double timeLimit);
    public abstract Move GetMoveDrafted(BoardStruct b, int depth);
    public abstract double TimeFunc(string fName);
    public abstract Dictionary<string, int> GetPartCounts();
}
