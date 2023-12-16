using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Search = V4.Search;

sealed class Version4 : ChessAbstract
{
    public override string Name { get; }
    public override string Version { get; }
    public Version4()
    {
        Name = "Move Sorting";
        Version = "4";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(3);
    }

    static void Main(string[] args) {
    }
}