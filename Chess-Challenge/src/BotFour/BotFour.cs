using ChessChallenge.API;
using System;
using System.Collections.Generic;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class BotFour : IChessBot
    {
        public Move Think(Board board, Timer timer)
        {
            //Get all legal moves
        Move[] moves = board.GetLegalMoves();
        var valueList = new int[moves.Length];
        int moveValue = 0;

        //Determine if MyBot is playing white
        bool playerIsWhite = (board.PlyCount % 2 == 0);

        //Check the number of pawns you currently have in play
        int numPawns = 8 - board.GetPieceList(PieceType.Pawn, playerIsWhite).Count;

        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues =
        {
            0,
            150 + numPawns*25,
            300,
            400,
            500,
            900,
            1000
        };

        //Loop through all legal moves
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //Evaluate the current move and update the current move value, creating a list of values as we go
            int currentValue = EvaluateMove(board, move, playerIsWhite, pieceValues);
            valueList[i] = currentValue;

            if (currentValue > moveValue)
            {
                moveValue = currentValue;
            }
        }

        //call Choose a Move to... Choose a Move
        int moveToUse = ChooseAMove(moveValue, valueList);
        return moves[moveToUse];
    }

    static int EvaluateMove(Board board, Move move, bool isWhite, int[] pieceValues)
    {
        //Stole this from EvilBot, sets current value to the value of the piece to start out (or 0 for no piece capture)
        Piece capturedPiece = board.GetPiece(move.TargetSquare);
        int currentValue = pieceValues[(int)capturedPiece.PieceType];

        //If the piece that's moving is the king, decentivise moving forward, but lay off this as turns pass, can even turn into a benefit for moving the king in late game
        if (move.MovePieceType == PieceType.King && !isWhite && move.StartSquare.Rank > move.TargetSquare.Rank)
        {
            currentValue -= 100 - board.PlyCount;
        }
        else if (move.MovePieceType == PieceType.King && isWhite && move.StartSquare.Rank < move.TargetSquare.Rank)
        {
            currentValue -= 100 - board.PlyCount;
        }

        //If the move is to promote a pawn, give the move a value boost based on the promoted piece type
        currentValue += move.IsPromotion ? pieceValues[(int)move.PromotionPieceType] : 0;

        //If you can castle, that's worth a little extra. Protect that King.
        currentValue += move.IsCastles ? 200 : 0;

        //This just generally incentivices pawn advancement, moreso the further in the game you get
        currentValue += move.MovePieceType == PieceType.Pawn ? (50 + board.PlyCount) : 0;

        board.MakeMove(move);

        //If it's checkmate, we basically just want to do that
        currentValue += board.IsInCheckmate() ? 99999 : 0;

        //If it puts them in check, it gets a bonus. But, and I actually originally did this on accident, but it really worked;
        //If it puts them in check and also captures, it's slightly less of a bonus, because those are already good moves
        currentValue += board.IsInCheck() ? (move.IsCapture ? 200 : 400) : 0;

        //And, if it would cause a draw, we disincentivise that, though there's often not a lot you can do about it
        currentValue -= board.IsDraw() ? 200 : 0;
        board.UndoMove(move);

        //This is probably my favorite part of my bot, the DangerValue function. I'll explain in detail when we get there.
        currentValue -= DangerValue(board, move, isWhite, pieceValues);

        return currentValue;
    }

    static int ChooseAMove(int moveValue, int[] valueList)
    {
        Random rng = new();
        List<int> indexList = new();

        //Loop through the list of values we made earlier, finding ones that match the moveValue we evaluated and adding them to a list
        for (int i = 0; i < valueList.Length; i++)
        {
            if (valueList[i] == moveValue)
            {
                indexList.Add(i);
            }
        }

        //index is a random index from the list we just created
        int index = rng.Next(indexList.Count);

        //If IndexList is empty, which is unlikely, we just return a random move index 
        if (indexList.Count == 0)
        {
            int move = rng.Next(valueList.Length);
            return move;
        }
        else
        {
            //Otherwise we return the index of the move randomly selected from all the moves that tied for highest value
            int move = indexList[index];
            return move;
        }
    }

    static int DangerValue(Board board, Move move, bool isWhite, int[] pieceValues)
    {
        //I needed a way to calculate the attacks that the enemy had available before making the move
        //...and I don't know how to use bitboards lol

        //Get the list of active squares
        List<Square> activeSquares = new(GetActiveSquares(board, isWhite));

        //Calculate Danger before, make move and calculate Danger after the move
        int DangerBefore = CountDangerBefore(board, activeSquares, pieceValues);
        board.MakeMove(move);
        int DangerAfter = CountDangerAfter(board, pieceValues);
        board.UndoMove(move);

        //Subtract the DangerAfter from the Danger before, giving a value that decentivizes putting major pieces in jeopardy
        //But also that incentivizes protecting those same pieces
        int danger = DangerAfter - DangerBefore;

        return danger;
    }

    static List<Square> GetActiveSquares(Board board, bool isWhite)
    {
        //Get list of all piece lists
        List<PieceList> activePieces = new(board.GetAllPieceLists());
        Piece piece;
        List<Square> activeWhiteSquares = new();
        List<Square> activeBlackSquares = new();

        //Loop through all piece lists
        for (int i = 0; i < activePieces.Count; i++)
        {
            //Loop through all pieces within each piece list
            for (int e = 0; e < activePieces[i].Count; e++)
            {
                //if it's white, add it's square to the list of active white squares
                if (activePieces[i].IsWhitePieceList)
                {
                    piece = activePieces[i].GetPiece(e);
                    activeWhiteSquares.Add(piece.Square);
                }
                //Same for black squares
                else
                {
                    piece = activePieces[i].GetPiece(e);
                    activeBlackSquares.Add(piece.Square);
                }
            }
        }

        //Then, depending on player color, return the proper list of active squares
        if (isWhite)
        {
            return activeWhiteSquares;
        }
        else
        {
            return activeBlackSquares;
        }
    }


    static int CountDangerBefore(Board board, List<Square> activeSquares, int[] pieceValues)
    {
        int dangerValue = 0;
        int numAttacks = 0;

        //Loop through active squares
        for (int i = 0; i < activeSquares.Count; i++)
        {
            //If that square is attacked by the opponenet, get the piece value, and if it's larger than the current danger value, update it
            if (board.SquareIsAttackedByOpponent(activeSquares[i]))
            {
                Piece piece = board.GetPiece(activeSquares[i]);
                int tempValue = pieceValues[(int)piece.PieceType];

                if (tempValue > dangerValue)
                {
                    dangerValue = tempValue;
                }
                //Also keeping track of the number of attacks
                numAttacks++;
            }
        }

        //Danger value ends up being the max value of the piece threatened, plus a value representing the number of attacks in general
        dangerValue += numAttacks * 50;

        return dangerValue;
    }

    static int CountDangerAfter(Board board, int[] pieceValues)
    {
        int dangerValue = 0;
        int numAttacks = 0;
        //Since we've already done MakeMove in the larger context, we can just use getlegalmoves to get opponents moves (thanks community!)
        Move[] captureMoves = board.GetLegalMoves(true);

        //Loop through all legal capture moves, get the piece value, update danger value
        for (int i = 0; i < captureMoves.Length; i++)
        {
            int tempValue = pieceValues[(int)captureMoves[i].CapturePieceType];

            if (tempValue > dangerValue)
            {
                dangerValue = tempValue;
            }
            numAttacks++;
        }
        //Danger value calculated the same as before, so that it mostly evens out when subtracting the two, unless something real bad is gonna happen+
        dangerValue += numAttacks * 50;

        return dangerValue;
    }
}
}