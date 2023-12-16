﻿using System;
using System.Collections.Generic;
using System.Text;
using Search = V2.Search;
using UnityEngine;

sealed class Version2 : ChessAbstract
{
    public override string Name { get; }
    public override string Version { get; }
    public Version2()
    {
        Name = "Quiessence";
        Version = "2";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(3);
    }

    static void Main(string[] args) {
    }
}