// 중원 평원 경매: 300년마다 고급 공법서·법보·단약·부적이 나온다. 부유한 수선자가 입찰하고 낙찰가는 세계에서 사라진다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>경매 규칙.</summary>
    public static class AuctionSystem
    {
        private const int BidderCount = 30;
        private const int AuctionPhase = 150;
        private const int MaxRecords = 30;

        private enum LotKind
        {
            Technique,
            Artifact,
            Longevity,
            Breakthrough,
            Thunder,
        }

        /// <summary>경매 해면 연다.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.Year % Balance.AuctionInterval == AuctionPhase)
            {
                Hold(world);
            }
        }

        /// <summary>다음 경매까지 남은 년수.</summary>
        /// <param name="year">연도.</param>
        /// <returns>년수.</returns>
        public static int YearsUntilNext(int year)
        {
            int phase = year % Balance.AuctionInterval;
            return phase < AuctionPhase ? AuctionPhase - phase : Balance.AuctionInterval - phase + AuctionPhase;
        }

        private static void Hold(World world)
        {
            List<Cultivator> bidders = new(world.Cultivators);
            bidders.Sort((left, right) => right.SpiritStones != left.SpiritStones
                ? right.SpiritStones.CompareTo(left.SpiritStones) : left.Id.CompareTo(right.Id));
            bidders.RemoveRange(Math.Min(BidderCount, bidders.Count), Math.Max(0, bidders.Count - BidderCount));
            List<string> lots = new();
            long total = 0;
            int count = world.Random.Range(3, 5);
            for (int index = 0; index < count; index++)
            {
                LotKind kind = (LotKind)world.Random.NextInt(5);
                int grade = kind == LotKind.Technique ? world.Random.Range(10, 16) : world.Random.Range(4, 5);
                long sold = Sell(world, bidders, kind, grade, lots);
                total += sold;
            }

            world.Auctions.Add(new AuctionRecord(world.Year, lots, total));
            if (world.Auctions.Count > MaxRecords)
            {
                world.Auctions.RemoveAt(0);
            }

            string text = "중원 평원 경매가 열렸다. 낙찰 " + lots.Count + "건, 총 영석 " + KoreanNumber.Compact(total) + ".";
            Narrator.Log(world, LogKind.Trade, text);
            if (total >= 50_000)
            {
                Narrator.Chronicle(world, ChronicleKind.Auction, text);
            }
        }

        private static long Sell(World world, List<Cultivator> bidders, LotKind kind, int grade, List<string> lots)
        {
            long value = Value(kind, grade);
            Cultivator? best = null;
            double bestBid = 0;
            double secondBid = value * 0.6;
            foreach (Cultivator bidder in bidders)
            {
                if (!Wants(bidder, kind, grade))
                {
                    continue;
                }

                double bid = Math.Min(bidder.SpiritStones * world.Random.Range(0.3, 0.7), value
                    * world.Random.Range(0.8, 2.0));
                if (bid > bestBid)
                {
                    secondBid = Math.Max(secondBid, bestBid);
                    bestBid = bid;
                    best = bidder;
                }
                else if (bid > secondBid)
                {
                    secondBid = bid;
                }
            }

            if (best is null || bestBid < value * 0.6)
            {
                return 0;
            }

            long price = (long)Math.Min(bestBid, Math.Max(value * 0.6, secondBid * 1.1));
            best.SpiritStones -= price;
            world.Ledger.Destroy(StoneSink.Auction, price);
            string item = Deliver(best, kind, grade);
            lots.Add(item + " → " + best.Name + " (" + KoreanNumber.Compact(price) + ")");
            Narrator.Log(world, LogKind.Trade, best.Name + "이(가) 경매에서 " + item + "을(를) 영석 "
                + KoreanNumber.Compact(price)
                + "에 낙찰받았다.", best, notable: true);
            return price;
        }

        private static long Value(LotKind kind, int grade)
        {
            return kind switch
            {
                LotKind.Technique => Techniques.MarketPrice(grade) * 3 / 2,
                LotKind.Artifact => 2000L * grade,
                LotKind.Longevity => 6000,
                LotKind.Breakthrough => 1500,
                _ => 900,
            };
        }

        private static bool Wants(Cultivator bidder, LotKind kind, int grade)
        {
            return kind switch
            {
                LotKind.Technique => bidder.TechniqueGrade < grade,
                LotKind.Artifact => bidder.ArtifactGrade < grade,
                LotKind.Longevity => Lifespans.AgeRatio(bidder) > 0.6
                    && bidder.LongevityPillsTaken < Lifespans.MaxLongevityPills,
                LotKind.Breakthrough => bidder.Stage == Realms.PeakStage,
                _ => bidder.Realm >= 3,
            };
        }

        private static string Deliver(Cultivator winner, LotKind kind, int grade)
        {
            switch (kind)
            {
                case LotKind.Technique:
                    winner.TechniqueGrade = grade;
                    return Techniques.Name(grade) + " 공법서";
                case LotKind.Artifact:
                    winner.ArtifactGrade = grade;
                    return Labels.ArtifactGrade(grade) + " 법보";
                case LotKind.Longevity:
                    winner.Pills[(int)PillKind.Longevity]++;
                    return "연수단";
                case LotKind.Breakthrough:
                    winner.Pills[(int)PillKind.Breakthrough] += 3;
                    return "파경단 세 알";
                default:
                    winner.Talismans[(int)TalismanKind.Thunder] += 3;
                    return "피뢰부 세 장";
            }
        }
    }
}
