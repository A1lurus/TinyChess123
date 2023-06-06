using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        int direction = (team == 0) ? 1 : -1;

        // ќдин в перед
        if (board[currentX,currentY + direction] == null)
        {
            r.Add(new Vector2Int(currentX, currentY + direction));
        }

        // ƒва в перед
        if (board[currentX, currentY + direction] == null)
        {
            // white
            if(team ==0 && currentY == 1 && board[currentX, currentY + direction * 2] == null)
            {
                r.Add(new Vector2Int(currentX, currentY + (direction * 2)));
            }
            // black
            if (team == 1 && currentY == 6 && board[currentX, currentY + direction * 2] == null)
            {
                r.Add(new Vector2Int(currentX, currentY + (direction * 2)));
            }
        }

        // kill
            // white
        if (currentX != tileCountX - 1)
        {
            if (board[currentX+1, currentY+direction] != null && board[currentX + 1, currentY + direction].team != team)
            {
                r.Add(new Vector2Int(currentX + 1, currentY + direction));
            }
        }
            // black
        if (currentX != 0)
        {
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != team)
            {
                r.Add(new Vector2Int(currentX - 1, currentY + direction));
            }
        }
        return r;
    }
    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> movelist, ref List<Vector2Int> availableMoves)
    {
        int direction = (team == 0) ? 1 : -1;

        if((team == 0 && currentY == 6) || (team == 1 && currentY == 1))
        {
            return SpecialMove.Promotion;
        }

        // En Passant
        if (movelist.Count > 0)
        {
            Vector2Int[] lastMove = movelist[movelist.Count - 1];
            ChessPieceType lastPieceType = board[lastMove[1].x, lastMove[1].y].type;
            bool isBPawn = lastPieceType == ChessPieceType.BPawn;
            bool isWPawn = lastPieceType == ChessPieceType.WPawn;

            if ((isBPawn || isWPawn) && Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2 && board[lastMove[1].x, lastMove[1].y].team != team && lastMove[1].y == currentY)
            {
                if (lastMove[1].x == currentX - 1)
                {
                    availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                    return SpecialMove.EnPassant;
                }
                if (lastMove[1].x == currentX + 1)
                {
                    availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                    return SpecialMove.EnPassant;
                }
            }
        }
        return base.GetSpecialMoves(ref board, ref movelist, ref availableMoves);
    }
}
