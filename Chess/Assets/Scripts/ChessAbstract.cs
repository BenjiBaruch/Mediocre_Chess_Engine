using System;
using UnityEngine;

public abstract class ChessAbstract : MonoBehaviour
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public bool Side { get; set; }
    public abstract Move GetMove(BoardStruct b, double timeLimit);
}
