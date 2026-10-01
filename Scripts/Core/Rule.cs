namespace SuperChess.Core;

/// <summary>
/// 走法生成与终局判定（移植自参考项目 Rule.java 的偏移表算法）：
/// 偏移表走法生成（含蹩马腿/塞象眼/过河兵/九宫限制/飞将），
/// 合法着 = 伪合法着过滤「走后被将军」与「走后将帅照脸」。
/// </summary>
public static class Rule
{
    // 区域编号：0 棋盘外、1 黑方半场、2 黑方九宫、3 红方半场、4 红方九宫。
    private static readonly int[][] Area =
    [
        [1, 1, 1, 2, 2, 2, 1, 1, 1],
        [1, 1, 1, 2, 2, 2, 1, 1, 1],
        [1, 1, 1, 2, 2, 2, 1, 1, 1],
        [1, 1, 1, 1, 1, 1, 1, 1, 1],
        [1, 1, 1, 1, 1, 1, 1, 1, 1],
        [3, 3, 3, 3, 3, 3, 3, 3, 3],
        [3, 3, 3, 3, 3, 3, 3, 3, 3],
        [3, 3, 3, 4, 4, 4, 3, 3, 3],
        [3, 3, 3, 4, 4, 4, 3, 3, 3],
        [3, 3, 3, 4, 4, 4, 3, 3, 3],
    ];

    // 偏移表行号：0 帅将、1 仕士、2 相象、3 象眼、4 马、5 马腿、6 未过河卒、7 过河卒、8 未过河兵、9 过河兵、10 反向马腿（将帅受攻检测用）。
    private static readonly int[][] OffsetX =
    [
        [0, 0, 1, -1],
        [1, 1, -1, -1],
        [2, 2, -2, -2],
        [1, 1, -1, -1],
        [1, 1, -1, -1, 2, 2, -2, -2],
        [0, 0, 0, 0, 1, 1, -1, -1],
        [0],
        [-1, 0, 1],
        [0],
        [-1, 0, 1],
        [1, 1, -1, -1, 1, 1, -1, -1],
    ];

    private static readonly int[][] OffsetY =
    [
        [1, -1, 0, 0],
        [1, -1, 1, -1],
        [2, -2, 2, -2],
        [1, -1, 1, -1],
        [2, -2, 2, -2, 1, -1, 1, -1],
        [1, -1, 1, -1, 0, 0, 0, 0],
        [1],
        [0, 1, 0],
        [-1],
        [0, -1, 0],
        [1, -1, 1, -1, 1, -1, 1, -1],
    ];

    /// <summary>
    /// 生成指定坐标棋子的全部合法走法（过滤走后被将军与将帅照脸）。
    /// 起点无子时返回空列表。
    /// </summary>
    public static List<Move> GetLegalMoves(Board board, Position from)
    {
        var moves = new List<Move>();
        var piece = board.GetPiece(from);
        if (!Piece.IsValid(piece))
        {
            return moves;
        }

        var red = Piece.IsRed(piece);
        foreach (var to in GetPseudoTargets(board, piece, from.X, from.Y))
        {
            if (!LeavesOwnKingUnsafe(board, from, to, piece, red))
            {
                moves.Add(new Move(from, to));
            }
        }

        return moves;
    }

    /// <summary>生成指定走子方（redSide）的全部合法走法。</summary>
    public static List<Move> GetAllLegalMoves(Board board, bool redSide)
    {
        var moves = new List<Move>();
        for (var y = 0; y < Board.Height; y++)
        {
            for (var x = 0; x < Board.Width; x++)
            {
                var piece = board.GetPiece(x, y);
                if (Piece.IsValid(piece) && Piece.IsRed(piece) == redSide)
                {
                    moves.AddRange(GetLegalMoves(board, new Position(x, y)));
                }
            }
        }

        return moves;
    }

    /// <summary>检查一步走法是否合法（起点有子且终点在合法走法集合内）。</summary>
    public static bool IsLegalMove(Board board, Move move)
    {
        if (!Piece.IsValid(board.GetPiece(move.From)))
        {
            return false;
        }

        return GetLegalMoves(board, move.From).Contains(move);
    }

    /// <summary>指定方将/帅当前是否被攻击（不含将帅照脸，照脸由 IsKingsFacing 判定）。</summary>
    public static bool IsInCheck(Board board, bool redSide)
    {
        var kingPos = board.FindKing(redSide);
        return kingPos is not null && IsKingInDanger(board, kingPos.Value, redSide);
    }

    /// <summary>指定方是否被将死（被将军且无合法着法）。</summary>
    public static bool IsCheckmate(Board board, bool redSide) =>
        IsInCheck(board, redSide) && GetAllLegalMoves(board, redSide).Count == 0;

    /// <summary>指定方是否被困毙（未被将军但无合法着法）。</summary>
    public static bool IsStalemate(Board board, bool redSide) =>
        !IsInCheck(board, redSide) && GetAllLegalMoves(board, redSide).Count == 0;

    /// <summary>将帅是否在同一纵线直接照面（中间无子，白脸将）。</summary>
    public static bool IsKingsFacing(Board board)
    {
        var redKing = board.FindKing(red: true);
        var blackKing = board.FindKing(red: false);
        if (redKing is null || blackKing is null || redKing.Value.X != blackKing.Value.X)
        {
            return false;
        }

        var top = Math.Min(redKing.Value.Y, blackKing.Value.Y);
        var bottom = Math.Max(redKing.Value.Y, blackKing.Value.Y);
        for (var y = top + 1; y < bottom; y++)
        {
            if (board.GetPiece(redKing.Value.X, y) != Piece.Empty)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>模拟走子后己方将帅是否不安全（被将军或将帅照脸）。</summary>
    private static bool LeavesOwnKingUnsafe(Board board, Position from, Position to, int piece, bool red)
    {
        // 在克隆局面上做「走子-还原」，避免每次模拟整盘深拷贝。
        var sim = board.Clone();
        var captured = sim.GetPiece(to);
        sim.SetPiece(from.X, from.Y, Piece.Empty);
        sim.SetPiece(to.X, to.Y, piece);

        var unsafeAfterMove = IsKingsFacing(sim) ||
            (IsKingInDanger(sim, red ? sim.FindKing(true)!.Value : sim.FindKing(false)!.Value, red));

        sim.SetPiece(to.X, to.Y, captured);
        sim.SetPiece(from.X, from.Y, piece);
        return unsafeAfterMove;
    }

    /// <summary>
    /// 生成棋子从 (fromX, fromY) 出发的全部伪合法落点（移植 PossibleToPositions）。
    /// 含蹩马腿/塞象眼/九宫/半场限制/飞将过滤，不含「走后被将军」过滤。
    /// </summary>
    private static List<Position> GetPseudoTargets(Board board, int piece, int fromX, int fromY)
    {
        var targets = new List<Position>();
        switch (piece)
        {
            case Piece.BlackKing:
            case Piece.RedKing:
                AddOffsetTargets(piece, fromX, fromY, board, num: 0, targets, palaceOnly: true);
                break;

            case Piece.BlackAdvisor:
            case Piece.RedAdvisor:
                AddOffsetTargets(piece, fromX, fromY, board, num: 1, targets, palaceOnly: true);
                break;

            case Piece.BlackBishop:
            case Piece.RedBishop:
                // 走田字且塞象眼，目标须留在己方半场（黑 1-2、红 3-4）。
                for (var i = 0; i < 4; i++)
                {
                    var toX = fromX + OffsetX[2][i];
                    var toY = fromY + OffsetY[2][i];
                    var area = InArea(toX, toY);
                    var selfRed = Piece.IsRed(piece);
                    if (area != 0 && area >= (selfRed ? 3 : 1) && area <= (selfRed ? 4 : 2) &&
                        !Piece.IsSameSide(piece, board.GetPiece(toX, toY)) &&
                        board.GetPiece(fromX + OffsetX[3][i], fromY + OffsetY[3][i]) == Piece.Empty)
                    {
                        targets.Add(new Position(toX, toY));
                    }
                }

                break;

            case Piece.BlackKnight:
            case Piece.RedKnight:
                // 走日字且蹩马腿。
                for (var i = 0; i < 8; i++)
                {
                    var toX = fromX + OffsetX[4][i];
                    var toY = fromY + OffsetY[4][i];
                    if (InArea(toX, toY) != 0 &&
                        !Piece.IsSameSide(piece, board.GetPiece(toX, toY)) &&
                        board.GetPiece(fromX + OffsetX[5][i], fromY + OffsetY[5][i]) == Piece.Empty)
                    {
                        targets.Add(new Position(toX, toY));
                    }
                }

                break;

            case Piece.BlackRook:
            case Piece.RedRook:
                AddRayTargets(piece, fromX, fromY, board, cannon: false, targets);
                break;

            case Piece.BlackCannon:
            case Piece.RedCannon:
                AddRayTargets(piece, fromX, fromY, board, cannon: true, targets);
                break;

            case Piece.BlackPawn:
            case Piece.RedPawn:
                {
                    var selfRed = Piece.IsRed(piece);
                    // 未过河（红 area 3 / 黑 area 1）只能前进；过河后可前进与左右平移。
                    var crossed = InArea(fromX, fromY) != (selfRed ? 3 : 1);
                    var num = selfRed ? (crossed ? 9 : 8) : (crossed ? 7 : 6);
                    for (var i = 0; i < OffsetX[num].Length; i++)
                    {
                        var toX = fromX + OffsetX[num][i];
                        var toY = fromY + OffsetY[num][i];
                        if (InArea(toX, toY) != 0 && !Piece.IsSameSide(piece, board.GetPiece(toX, toY)))
                        {
                            targets.Add(new Position(toX, toY));
                        }
                    }

                    break;
                }
        }

        return targets;
    }

    /// <summary>按偏移表生成落点；palaceOnly 为真时落点须在本方九宫内，帅另需过滤飞将。</summary>
    private static void AddOffsetTargets(int piece, int fromX, int fromY, Board board, int num,
        List<Position> targets, bool palaceOnly)
    {
        var red = Piece.IsRed(piece);
        var palaceArea = red ? 4 : 2;
        for (var i = 0; i < OffsetX[num].Length; i++)
        {
            var toX = fromX + OffsetX[num][i];
            var toY = fromY + OffsetY[num][i];
            var area = InArea(toX, toY);
            if (area == 0 || (palaceOnly && area != palaceArea) ||
                Piece.IsSameSide(piece, board.GetPiece(toX, toY)))
            {
                continue;
            }

            // 帅将落点不得与对方将帅直接照面（飞将过滤）。
            if (num == 0 && FlyKingTarget(piece, toX, toY, board) is not null)
            {
                continue;
            }

            targets.Add(new Position(toX, toY));
        }
    }

    /// <summary>车炮沿四个方向生成落点：车遇子可吃后止；炮空移无遮挡、吃子须恰一炮架。</summary>
    private static void AddRayTargets(int piece, int fromX, int fromY, Board board, bool cannon,
        List<Position> targets)
    {
        (int dx, int dy)[] directions = [(0, 1), (0, -1), (-1, 0), (1, 0)];
        foreach (var (dx, dy) in directions)
        {
            var x = fromX + dx;
            var y = fromY + dy;
            var screenCount = 0;
            while (InArea(x, y) != 0)
            {
                var target = board.GetPiece(x, y);
                if (screenCount == 0)
                {
                    if (target == Piece.Empty)
                    {
                        targets.Add(new Position(x, y));
                    }
                    else
                    {
                        screenCount = 1;
                        // 车：遇到的第一个子即可吃并停止；炮：该子成为炮架，继续扫描。
                        if (!cannon)
                        {
                            if (!Piece.IsSameSide(piece, target))
                            {
                                targets.Add(new Position(x, y));
                            }

                            break;
                        }
                    }
                }
                else if (cannon)
                {
                    if (target != Piece.Empty)
                    {
                        // 炮吃子：越过恰一个炮架后的第一个子。
                        if (!Piece.IsSameSide(piece, target))
                        {
                            targets.Add(new Position(x, y));
                        }

                        break;
                    }
                }

                x += dx;
                y += dy;
            }
        }
    }

    /// <summary>将/帅位于 (x, y) 时沿纵向是否与对方将帅直接照面；照面返回对方将帅位置。</summary>
    private static Position? FlyKingTarget(int piece, int x, int y, Board board)
    {
        var red = Piece.IsRed(piece);
        var step = red ? -1 : 1;
        for (var i = y + step; i >= 0 && i < Board.Height; i += step)
        {
            var target = board.GetPiece(x, i);
            if (target != Piece.Empty)
            {
                return target == (red ? Piece.BlackKing : Piece.RedKing) ? new Position(x, i) : null;
            }
        }

        return null;
    }

    /// <summary>指定方将帅是否被对方攻击（马/车/炮/卒兵四类，移植 isJiangShuaiInDanger）。</summary>
    private static bool IsKingInDanger(Board board, Position kingPos, bool red)
    {
        var x = kingPos.X;
        var y = kingPos.Y;

        // 被对方马攻击（反向蹩马腿：以将帅为中心检查马腿位置）。
        for (var i = 0; i < 8; i++)
        {
            var toX = x + OffsetX[4][i];
            var toY = y + OffsetY[4][i];
            var blockX = x + OffsetX[10][i];
            var blockY = y + OffsetY[10][i];
            var attackerKnight = red ? Piece.BlackKnight : Piece.RedKnight;
            if (InArea(toX, toY) != 0 &&
                board.GetPiece(toX, toY) == attackerKnight &&
                board.GetPiece(blockX, blockY) == Piece.Empty)
            {
                return true;
            }
        }

        // 被对方车攻击。
        if (AttackableByRookOrCannon(board, x, y, red ? Piece.BlackRook : Piece.RedRook))
        {
            return true;
        }

        // 被对方炮攻击。
        if (AttackableByRookOrCannon(board, x, y, red ? Piece.BlackCannon : Piece.RedCannon))
        {
            return true;
        }

        // 被对方卒/兵攻击：横向两侧或正前方一格。
        var attackerPawn = red ? Piece.BlackPawn : Piece.RedPawn;
        var forward = red ? -1 : 1;
        return board.GetPiece(x - 1, y) == attackerPawn ||
               board.GetPiece(x + 1, y) == attackerPawn ||
               board.GetPiece(x, y + forward) == attackerPawn;
    }

    /// <summary>
    /// (x, y) 是否被指定车/炮攻击：以「被攻击方颜色」的同类棋子生成 (x, y) 处的伪合法落点
    /// （车炮走法双方对称，仅同侧判定依赖颜色），若任一落点上恰为该攻击者则处于其攻击范围。
    /// 与参考项目 attackableByJuPao 语义一致：将帅留在原位参与同侧判定，攻击者因异色总可被"吃"。
    /// </summary>
    private static bool AttackableByRookOrCannon(Board board, int x, int y, int attacker)
    {
        var probe = attacker switch
        {
            Piece.BlackRook => Piece.RedRook,
            Piece.RedRook => Piece.BlackRook,
            Piece.BlackCannon => Piece.RedCannon,
            _ => Piece.BlackCannon,
        };
        return GetPseudoTargets(board, probe, x, y).Any(pos => board.GetPiece(pos) == attacker);
    }

    /// <summary>查询 (x, y) 的区域编号（越界返回 0）。</summary>
    private static int InArea(int x, int y) =>
        x >= 0 && x < Board.Width && y >= 0 && y < Board.Height ? Area[y][x] : 0;
}
