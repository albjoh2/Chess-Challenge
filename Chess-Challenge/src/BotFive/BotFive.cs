using ChessChallenge.API;
using System.Linq;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class BotFive : IChessBot
    {
     public Move Think(Board board, Timer timer)
    {
        var legs = board.GetLegalMoves();
        var values = legs
            .Select(x=> 
                new Random().Next(3)
                -abs(x.StartSquare.Index-board.GetKingSquare(board.IsWhiteToMove).Index) * (board.PlyCount<60?1:-1))
            .ToList();
        foreach (Move leg in legs)
        {
            board.MakeMove(leg);
            if (board.IsInCheckmate() || leg.IsCapture) return leg;
            board.UndoMove(leg);
        }
        return legs[values.FindIndex(x=>x==values.Max())];
    }
    int abs(int x) => x < 0 ? -x : x;
}
}