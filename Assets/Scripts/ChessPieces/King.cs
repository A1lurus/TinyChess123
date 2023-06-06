using System.Collections.Generic;
using UnityEngine;

public class King : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        int[] x = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] y = { 1, 1, 0, -1, -1, -1, 0, 1 };
        for (int i = 0; i < 8; i++)
        {
            if ((currentX + x[i] < tileCountX && currentY + y[i] < tileCountY)
                && (currentX + x[i] >= 0 && currentY + y[i] >= 0))
            {
                if (board[currentX + x[i], currentY + y[i]] == null)
                {
                    r.Add(new Vector2Int(currentX + x[i], currentY + y[i]));
                }
                else if (board[currentX + x[i], currentY + y[i]].team != team)
                {
                    r.Add(new Vector2Int(currentX + x[i], currentY + y[i]));
                }
            }
        }
        return r;
    }
    // Castling
    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> movelist, ref List<Vector2Int> availableMoves)
    {
        SpecialMove r = SpecialMove.None;
        int row = (team == 1) ? 0 : 7;

        var kingMove = movelist.Find(m => m[0].x == 4 && m[0].y == ((team == 0) ? 0 : 7));
        var leftRook = movelist.Find(m => m[0].x == 0 && m[0].y == ((team == 0) ? 0 : 7));
        var rightRook = movelist.Find(m => m[0].x == 7 && m[0].y == ((team == 0) ? 0 : 7));

        if (kingMove == null && currentX == 4)
        {
            ChessPieceType rookType = (team == 0) ? ChessPieceType.WRook : ChessPieceType.BRook;
            int rookY = (team == 0) ? 0 : 7;

            if (leftRook == null && board[0, rookY].type == rookType && board[0, rookY].team == team &&
                board[3, rookY] == null && board[2, rookY] == null && board[1, rookY] == null)
            {
                availableMoves.Add(new Vector2Int(2, rookY));
                r = SpecialMove.Castling;
            }

            if (rightRook == null && board[7, rookY].type == rookType && board[7, rookY].team == team &&
                board[5, rookY] == null && board[6, rookY] == null)
            {
                availableMoves.Add(new Vector2Int(6, rookY));
                r = SpecialMove.Castling;
            }
        }
        return r;
    }
}
