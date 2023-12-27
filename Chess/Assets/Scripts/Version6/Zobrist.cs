using UnityEngine;
using System.Security.Cryptography;
using System;

namespace V6 
{
    public class Zobrist 
    {
        public static long[,] whitePieceHash;
        public static long[,] blackPieceHash;
        public static long blackToMoveHash;
        public static long[] castleRightsHash;
        public static long[] pawnLeapFilesHash;
        public readonly int[,] Transposition;
        public int keySize;
        readonly long keyMask;
        readonly int tableSize;
        public enum Type {
            NoEntry,
            PrevAlpha,
            HashMove
        }
        static readonly Tuple<Type, int> notFound = new(Type.NoEntry, 0);
        static RNGCryptoServiceProvider RNG;

        public Zobrist(int keySize) 
        {
            this.keySize = keySize;
            keyMask = 0;
            for (int i = 0; i < keySize; i++) {
                keyMask |= 1L << i;
            }
            tableSize = 1 << keySize;
            Transposition = new int[tableSize, 4];
        }

        public void ClearTable() 
        {
            for (int i = 0; i < tableSize; i++) {
                Transposition[i, 0] = 0;
                Transposition[i, 1] = 0;
                Transposition[i, 2] = 0;
                Transposition[i, 3] = 0;
            } 
        }

        int SearchEntry(int key, int check) 
        {
            int offset = 0;
            while (true) {
                int value = Transposition[key + offset, 0];
                if (value == 0)
                    return -1;
                if (value == check)
                    return key + offset;
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
        }

        int SearchBlank(int key) 
        {
            int offset = 0;
            while (Transposition[key + offset, 0] != 0) {
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
            return key + offset;
        }

        int SearchEntryOrBlank(int key, int check) 
        {
            int offset = 0;
            while (true) {
                int value = Transposition[key + offset, 0];
                if (value == 0 || value == check)
                    return key + offset;
                if (key + offset < tableSize) {
                    offset++;
                } 
                else if (offset == -1) {
                    return -1;
                } 
                else {
                    offset = key - tableSize;
                }
            }
        }

        public Tuple<Type, int> Read(long hash, int minDepth) 
        {
            int key = (int)(hash & keyMask);
            int check = (int)(hash >> 32);
            int index = SearchEntry(key, check);
            if (index == -1)
                return notFound;
            if (Transposition[index, 2] < minDepth)
                return new(Type.HashMove, Transposition[index, 3]);
            return new(Type.PrevAlpha, Transposition[index, 1]);
        }

        public bool Write(long hash, int score, int depth, int hashMove) 
        {
            int key = (int)(hash & keyMask);
            int check = (int)(hash >> 32);
            int index = SearchEntryOrBlank(key, check);
            if (index == -1) 
                return false;
            Transposition[index, 0] = check;
            Transposition[index, 1] = score;
            Transposition[index, 2] = depth;
            Transposition[index, 3] = hashMove;
            return true;
        }

        public static void Initialize() 
        {
            // help from https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rngcryptoserviceprovider?view=net-8.0&redirectedfrom=MSDN
            // help from https://www.chessprogramming.org/Zobrist_Hashing
            RNG = new RNGCryptoServiceProvider();
            whitePieceHash = new long[8,64];
            blackPieceHash = new long[8,64];
            for (int i = 0; i < 7; i++) {
                for (int j = 0; j < 64; j++) {
                    whitePieceHash[i,j] = GetRandom();
                    blackPieceHash[i,j] = GetRandom();
                }
            }
            blackToMoveHash = GetRandom();
            castleRightsHash = new long[16];
            for (int i = 0; i < 16; i++) {
                castleRightsHash[i] = GetRandom();
            }
            pawnLeapFilesHash = new long[9];
            for (int i = 0; i < 9; i++) {
                pawnLeapFilesHash[i] = GetRandom();
            }
        }

        public static long GetRandom() 
        {
            var bytes = new byte[8];
            RNG.GetBytes(bytes);
            long hash = 0;
            foreach (byte b in bytes) {
                hash = (hash << 8) + b;
            }
            return hash;
        }

        public static long HashBoard(MoveGen board) 
        {
            long hash = board.WhiteToMove ? 0 : blackToMoveHash;
            int[] IntBoard = board.IntBoard;
            for (int i = 0; i < 64; i++) {
                if ((IntBoard[i] & 0b11000) == 0b01000) {
                    hash ^= whitePieceHash[IntBoard[i] & 0b111, i];
                }
                else if ((IntBoard[i] & 0b11000) == 0b10000) {
                    hash ^= blackPieceHash[IntBoard[i] & 0b111, i];
                }
            }
            hash ^= castleRightsHash[board.castlingRights];
            hash ^= pawnLeapFilesHash[board.pawnLeapFile];
            return hash;
        }
    }
}