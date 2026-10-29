// 사기 탭: 연대기·세계 기록·전쟁사·대비무·비승록·경매 기록을 20건씩 읽는다.
using System.Collections.Generic;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>보존된 세계의 사건과 최초·최연소 기록을 탐색한다.</summary>
    internal sealed class HistoryPage : PageBase
    {
        private static readonly string[] s_tabs = { "연대기", "세계 기록", "전쟁사", "대비무", "비승록", "경매" };
        private readonly RowList _rows;
        private readonly Pager _pager;
        private int _tab;

        /// <summary>사기 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal HistoryPage(INavigator navigator) : base(navigator)
        {
            _rows = new RowList(navigator.Open);
            _pager = new Pager(20, navigator.Invalidate);
            ChipBar tabs = new(s_tabs, index =>
            {
                _tab = index;
                _pager.Reset();
                navigator.Invalidate();
            });
            Body.Children.Add(Ui.Section("천하의 사기", tabs.View, _pager.View, _rows.View));
        }

        /// <inheritdoc />
        internal override string Name => "사기";

        /// <inheritdoc />
        internal override string Glyph => "史";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            List<RowModel> rows = BuildRows(world);
            _pager.SetTotal(rows.Count, "건");
            _rows.Update(rows.GetRange(_pager.Start, System.Math.Min(_pager.PageSize, rows.Count - _pager.Start)));
        }

        private List<RowModel> BuildRows(World world)
        {
            List<RowModel> rows = new();
            switch (_tab)
            {
                case 0:
                    for (int index = world.Chronicle.Entries.Count - 1; index >= 0; index--)
                    {
                        rows.Add(Rows.Chronicle(world.Chronicle.Entries[index]));
                    }

                    break;
                case 1:
                    AddRecords(world.Records, rows);
                    break;
                case 2:
                    AddWars(world, rows);
                    break;
                case 3:
                    AddTournaments(world, rows);
                    break;
                case 4:
                    AddImmortals(world, rows);
                    break;
                default:
                    AddAuctions(world, rows);
                    break;
            }

            return rows;
        }

        private static void AddImmortals(World world, List<RowModel> rows)
        {
            foreach (ImmortalRecord record in world.Immortals)
            {
                rows.Add(new RowModel
                {
                    Title = record.Name,
                    Value = Fmt.Years(record.Year),
                    Subtitle = record.Affiliation + " · " + record.Lives + "생 · " + record.Age + "세",
                    Link = SheetLink.ToCultivator(record.SoulId)
                });
            }

            rows.Reverse();
        }

        private static void AddAuctions(World world, List<RowModel> rows)
        {
            foreach (AuctionRecord record in world.Auctions)
            {
                rows.Add(new RowModel
                {
                    Title = Fmt.Years(record.Year) + " 중원 경매",
                    Subtitle = string.Join("\n", record.Lots),
                    Value = Fmt.Short(record.Total),
                    Caption = "영석"
                });
            }

            rows.Reverse();
        }

        private static void AddRecords(WorldRecords records, List<RowModel> rows)
        {
            for (int realm = 1; realm < Realms.Count; realm++)
            {
                AddRecord(rows, Realms.Name(realm) + " 최초", records.FirstReach[realm]);
                AddRecord(rows, Realms.Name(realm) + " 최연소", records.YoungestReach[realm]);
            }

            AddRecord(rows, "최장수", records.LongestLife);
            AddRecord(rows, "최다 윤회", records.MostLives);
            AddRecord(rows, "최고 전투력", records.HighestPower);
            AddRecord(rows, "최다 승리", records.MostWins);
            AddRecord(rows, "최다 영석", records.Richest);
            AddRecord(rows, "최다 대비무 우승", records.MostTournamentWins);
        }

        private static void AddRecord(List<RowModel> rows, string label, RecordEntry? record)
        {
            rows.Add(new RowModel
            {
                Title = label + ": " + (record?.Name ?? "미달성"),
                Subtitle = record?.Detail,
                Value = record is null ? "—" : Fmt.Number(record.Value),
                Caption = record is null ? string.Empty : Fmt.Years(record.Year),
                Link = SheetLink.ToCultivator(record?.SoulId ?? 0)
            });
        }

        private static void AddWars(World world, List<RowModel> rows)
        {
            for (int index = world.Wars.Count - 1; index >= 0; index--)
            {
                WarRecord war = world.Wars[index];
                rows.Add(new RowModel
                {
                    Title = "제" + war.Number + "차 정마대전",
                    Subtitle = Fmt.Years(war.StartYear) + "~" + Fmt.Years(war.EndYear) + " · 영웅 " + war.HeroName,
                    Value = war.VictorSectId == 0 ? "무승부" : world.FindSect(war.VictorSectId)?.Name,
                    Link = SheetLink.ToWar(war.Number)
                });
            }
        }

        private static void AddTournaments(World world, List<RowModel> rows)
        {
            for (int index = world.Tournaments.Count - 1; index >= 0; index--)
            {
                TournamentRecord record = world.Tournaments[index];
                rows.Add(new RowModel
                {
                    Title = "제" + record.Number + "회 천하 대비무",
                    Subtitle = "우승 " + record.ChampionName + " · " + record.ChampionAffiliation,
                    Value = Fmt.Years(record.Year),
                    Link = SheetLink.ToTournament(record.Number)
                });
            }
        }
    }
}
