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
        Name = "Basic Transposition Table";
        Version = "4";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(2);
    }

    static void Main(string[] args) {
    }
}