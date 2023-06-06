using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.5f;
    [SerializeField] private float deathSpacing = 0.5f;
    [SerializeField] private float dragOffset = 1f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;

    [Header("Prefabs and Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;

    //Logic
    private ChessPiece[,] chessPieces;
    private ChessPiece currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlack = new List<ChessPiece>();
    private const int Tile_count_x = 8;
    private const int Tile_count_y = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    //Multi logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];

    private void Start()
    {
        isWhiteTurn = true;
        
        GenerateAllTiles(tileSize, Tile_count_x, Tile_count_y);
        SpawnAllPieces();
        PositiAllPieces();

        RegisterEvents();
    }

    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            // Отримання індексу клітки яку я натиснув
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // Якщо ми наводимо стиль після того, як не наводимо жодної плитки
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }
            // Якщо наводили плитку, змінить змінну "one"
            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // Якщо затиснути кнопку миші
            if (Input.GetMouseButtonDown(0))
            {
                if(chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1))
                    {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, Tile_count_x, Tile_count_y);
                        // Спецефічні ходи 
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
            }
            // Якщо відпустити кнопку миші
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);
                if (ContainsValidMove(ref availableMoves, new Vector2Int(hitPosition.x, hitPosition.y)))
                {
                    MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    //Net implementation
                    NetMakeMove mm = new NetMakeMove();
                    mm.originalX = previousPosition.x;
                    mm.originalY = previousPosition.y;
                    mm.destinationX = hitPosition.x;
                    mm.destinationY = hitPosition.y;
                    mm.teamId = currentTeam;
                    Client.Instance.SendToServer(mm);
                }
                else
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                    currentlyDragging = null;
                    RemovehighlightTiles();
                }
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemovehighlightTiles();
            }
        }

        // Переміщення фігури за курсором
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
            {
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            }
        }
    }

    //Generate board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
        {
            for (int y = 0; y < tileCountY; y++)
            {
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
            }
        }
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //Spawning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[Tile_count_x, Tile_count_y];

        int whiteTeam = 0, blackTeam = 1;
        //white team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.WRook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.WHorse, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.WBishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.WQueen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.WKing, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.WBishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.WHorse, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.WRook, whiteTeam);
        for (int i = 0; i < Tile_count_x; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.WPawn, whiteTeam);
        }

        //Black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.BRook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.BHorse, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.BBishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.BQueen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.BKing, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.BBishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.BHorse, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.BRook, blackTeam);
        for (int i = 0; i < Tile_count_x; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.BPawn, blackTeam);
        }
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];

        return cp;
    }

    //Positioning
    private void PositiAllPieces()
    {
        for (int x = 0; x < Tile_count_x; x++)
        {
            for(int y = 0; y < Tile_count_y; y++)
            {
                if (chessPieces[x,y] != null)
                {
                    PositionSinglePiece(x, y, true);
                }
            }
        }
    }
    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }

    //HighlightTiles
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }
    private void RemovehighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        }
        availableMoves.Clear();
    }

    // CheckMate
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    public void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
    }
    public void OnRematchButton()
    {
        if (localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);

            GameReset();
        }
        else
        {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);
        }
    }
    public void GameReset()
    {
        // UI
        rematchButton.interactable = true;

        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);
        
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        // Fields reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;

        //Clean up
        for (int x = 0; x < Tile_count_x; x++)
        {
            for (int y = 0; y < Tile_count_y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    Destroy(chessPieces[x, y].gameObject);
                }
                chessPieces[x, y] = null;
            }
        }
        for (int i = 0; i < deadWhites.Count; i++)
        {
            Destroy(deadWhites[i].gameObject);
        }
        for (int i = 0; i < deadBlack.Count; i++)
        {
            Destroy(deadBlack[i].gameObject);
        }
        deadWhites.Clear();
        deadBlack.Clear();
        SpawnAllPieces();
        PositiAllPieces();
        isWhiteTurn = true;
    }
    public void OnMenuButton()
    {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        if (localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);

            GameReset();
        }

        GameReset();

        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutDownRelay", 1.0f);

        // Reset some values
        playerCount = -1;
        currentTeam = -1;
    }

    // Special Moves
    private void ProcessSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if (myPawn.currentX == enemyPawn.currentX)
            {
                if (myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if (enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3((8 * tileSize) + 0.3f, yOffset, -1 * tileSize) - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlack.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3((-1 * tileSize) - 0.3f, yOffset, 8 * tileSize) - bounds
                            + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlack.Count);
                    }
                }
                chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
            }
        }

        if (specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastmove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = chessPieces[lastmove[1].x, lastmove[1].y];

            if(targetPawn.type == ChessPieceType.BPawn || targetPawn.type == ChessPieceType.WPawn)
            {
                if (targetPawn.team == 0 && lastmove[1].y == 7)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.WQueen, 0);
                    Destroy(chessPieces[lastmove[1].x, lastmove[1].y].gameObject);
                    chessPieces[lastmove[1].x, lastmove[1].y] = newQueen;
                    PositionSinglePiece(lastmove[1].x, lastmove[1].y, true);
                }
                if (targetPawn.team == 1 && lastmove[1].y == 0)
                {
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.BQueen, 1);
                    Destroy(chessPieces[lastmove[1].x, lastmove[1].y].gameObject);
                    chessPieces[lastmove[1].x, lastmove[1].y] = newQueen;
                    PositionSinglePiece(lastmove[1].x, lastmove[1].y, true);
                }
            }
        }

        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            int rookY = (lastMove[1].y == 0) ? 0 : 7;

            if (lastMove[1].x == 2)
            {
                ChessPiece rook = chessPieces[0, rookY];
                chessPieces[3, rookY] = rook;
                PositionSinglePiece(3, rookY);
                chessPieces[0, rookY] = null;
            }
            else if (lastMove[1].x == 6)
            {
                ChessPiece rook = chessPieces[7, rookY];
                chessPieces[5, rookY] = rook;
                PositionSinglePiece(5, rookY);
                chessPieces[7, rookY] = null;
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for(int x = 0; x < Tile_count_x; x++)
        {
            for(int y = 0; y < Tile_count_y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x,y].type == ChessPieceType.BKing || chessPieces[x, y].type == ChessPieceType.WKing)
                    {
                        if(chessPieces[x,y].team == currentlyDragging.team)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                }
            }
        }
        SimulatedMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }
    private void SimulatedMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        //Збережіть поточні значення, щоб скинути їх після виклику функції
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        //Проходимо всі ходи, симулюємо їх і перевіряємо, чи все під контролем
        for(int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            // Моделюємо ходи короля
            if (cp.type == ChessPieceType.BKing || cp.type == ChessPieceType.WKing)
            {
                kingPositionThisSim = new Vector2Int(simX, simY);
            }
            // Копіюємо [,], а не посилання
            ChessPiece[,] simulation = new ChessPiece[Tile_count_x, Tile_count_y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for(int x = 0; x < Tile_count_x; x++)
            {
                for(int y = 0;y < Tile_count_y; y++)
                {
                    if (chessPieces[x,y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if(simulation[x,y].team != cp.team)
                        {
                            simAttackingPieces.Add(simulation[x, y]);
                        }
                    }
                }
            }
            // Моделювання руху
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;
            // Чи була одна з фігур зруйнована під час нашої симуляції
            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);
            if (deadPiece != null)
            {
                simAttackingPieces.Remove(deadPiece);
            }
            // Отримати всі ходи змодельованих атакуючих фігур
            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, Tile_count_x, Tile_count_y);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }
            // Чи є у короля проблеми? якщо так, то зніміть хід
            if(ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }
            // відновлення актуальних даних CP
            cp.currentX = actualX;
            cp.currentY = actualY;
        }
        //Видалити з поточного доступного списку переміщень
        for (int i = 0; i < movesToRemove.Count; i++)
        {
            moves.Remove(movesToRemove[i]);
        }
    }
    private bool CheckForCheckmate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < Tile_count_x; x++)
        {
            for (int y = 0; y < Tile_count_y; y++)
            {
                if (chessPieces[x, y] != null)
                {
                    if (chessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[x, y]);
                        if(chessPieces[x,y].type == ChessPieceType.BKing || chessPieces[x, y].type == ChessPieceType.WKing)
                        {
                            targetKing = chessPieces[x, y];
                        }
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[x, y]);
                    }
                }
            }
        }
        // Дізнаэмось чи атакують короля зараз
        List<Vector2Int> currentAvailebleMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, Tile_count_x, Tile_count_y);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                currentAvailebleMoves.Add(pieceMoves[b]);
            }
        }
        if (ContainsValidMove(ref currentAvailebleMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            for(int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, Tile_count_x, Tile_count_y);
                SimulatedMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);
                
                if (defendingMoves.Count != 0)
                {
                    return false;
                }
            }
            return true; // Checkmate exit
        } 

        return false;
    }

    //Operation
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
            {
                return true;
            }
        }
        return false;
    }
    private void MoveTo(int originalX, int originalY, int x, int y)
    {
        ChessPiece cp = chessPieces[originalX, originalY];
        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        // Визначити чи є інша фігура
        if (chessPieces[x,y] != null)
        {
            ChessPiece ocp = chessPieces[x,y];
            if (cp.team == ocp.team)
            {
                return;
            }
            // якщо ворожа фігура
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.WKing)
                {
                    CheckMate(1);
                }
                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3((8 * tileSize) + 0.3f, yOffset, -1 * tileSize) - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.BKing)
                {
                    CheckMate(0);
                }
                deadBlack.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3((-1 * tileSize) - 0.3f, yOffset, 8 * tileSize) - bounds
                    + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlack.Count);
            }
        }
        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;
        PositionSinglePiece(x, y);
        isWhiteTurn = !isWhiteTurn;
        if (localGame)
        {
            currentTeam = (currentTeam == 0) ? 1 : 0;
        }
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x,y)});

        ProcessSpecialMove();

        if (currentlyDragging)
        {
            currentlyDragging = null;
        }
        RemovehighlightTiles();

        if (CheckForCheckmate())
        {
            CheckMate(cp.team);
        }
        return;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < Tile_count_x; x++)
        {
            for (int y = 0; y < Tile_count_y; y++)
            {
                if (tiles[x, y] == hitInfo)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return -Vector2Int.one;
    }

    #region
    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.C_WELCOME += OnWelcomeClient;

        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;

        NetUtility.C_START_GAME += OnStartGameClient;

        NetUtility.S_REMATCH += OnRematchServer;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }

    private void UnRegisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.C_WELCOME -= OnWelcomeClient;

        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;

        NetUtility.C_START_GAME -= OnStartGameClient;

        NetUtility.S_REMATCH -= OnRematchServer;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }

    //Server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        // Клієнт приєднався, присвоїти команду та повернути повідомлення йому
        NetWelcome nw = msg as NetWelcome;
        // Присвоэння команди
        nw.AssignedTeam = ++playerCount;
        //Повернути клієнту
        Server.Instance.SendToClient(cnn, nw);

        // Якщо заповнені команди
        if (playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }

    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        // отримуэмо повыдомлення та транслюэмо назад
        NetMakeMove mm = msg as NetMakeMove;

        // Отримуйте та просто транслюйте його назад
        Server.Instance.Broadcast(msg);
    }
    private void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {
        // Отримуйте та просто транслюйте його назад
        Server.Instance.Broadcast(msg);
    }

    //Client
    private void OnWelcomeClient(NetMessage msg)
    {
        //Підключення повідомлення
        NetWelcome nw = msg as NetWelcome;

        //Команда
        currentTeam = nw.AssignedTeam;

        Debug.Log($"My assigned team is {nw.AssignedTeam}");

        if (localGame && currentTeam == 0)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnStartGameClient(NetMessage msg)
    {
        //Зміна камери
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }
    private void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove mm = msg as NetMakeMove;

        Debug.Log($"MM : {mm.teamId} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

        if (mm.teamId != currentTeam)
        {
            ChessPiece target = chessPieces[mm.originalX, mm.originalY];

            availableMoves = target.GetAvailableMoves(ref chessPieces, Tile_count_x, Tile_count_y);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
            
            MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);
        }
    }
    private void OnRematchClient(NetMessage msg)
    {
        // Recieve the connection message
        if (localGame)
        {
            NetRematch wrm = msg as NetRematch;

            rematchIndicator.transform.GetChild((wrm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if (wrm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }

            NetRematch brm = msg as NetRematch;
            rematchIndicator.transform.GetChild((brm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if (brm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }
        }

        NetRematch rm = msg as NetRematch;

        // Set the boolean for rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;

        // Activate the piece of UI
        if (rm.teamId != currentTeam)
        {
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if (rm.wantRematch != 1)
            {
                rematchButton.interactable = false;
            }
        }

        // If both player wants to rematch
        if (playerRematch[0] && playerRematch[1])
            GameReset();
        //// Отримуємо повідомлення підключення
        //NetRematch rm = msg as NetRematch;

        //// Встановлюэмо булеве значення для матча
        //playerRematch[rm.teamId] = rm.wantRematch == 1;

        //// Активація частини інтерфейса
        //if (rm.teamId != currentTeam)
        //{
        //    rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
        //    if (rm.wantRematch != 1)
        //    {
        //        rematchButton.interactable = false;
        //    }
        //}

        //// Якщо згоден з rematch
        //if (playerRematch[0] && playerRematch[1])
        //{
        //    GameReset();
        //}
    }

    //
    private void ShutdownRelay()
    {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }
    private void OnSetLocalGame(bool obj)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = obj;
    }
    #endregion
}