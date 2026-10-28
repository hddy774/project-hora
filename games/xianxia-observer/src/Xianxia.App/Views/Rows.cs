// 여러 화면이 같이 쓰는 목록 줄 모양: 수선자, 사건 기록, 연대기. 같은 대상은 어디서나 같은 모양으로 보인다.
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>목록 줄 만들기.</summary>
    internal static class Rows
    {
        /// <summary>수선자 줄.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="place">순위(0이면 표시 안 함).</param>
        /// <param name="value">오른쪽 값.</param>
        /// <param name="caption">값 설명.</param>
        /// <returns>줄 내용.</returns>
        internal static RowModel Cultivator(World world, Cultivator cultivator, int place, string? value,
            string? caption)
        {
            string star = world.IsFollowed(cultivator.Id) ? " ★" : string.Empty;
            return new RowModel
            {
                Title = cultivator.DisplayName + star,
                Subtitle = Fmt.Affiliation(world, cultivator) + " · " + Fmt.Age(cultivator) + " · "
                    + Labels.Of(cultivator.LastAction),
                Lead = place > 0 ? place.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                LeadColor = Palette.Place(place),
                Value = value,
                Caption = caption,
                Accent = Palette.Sect(cultivator.SectId),
                Badges = new[]
                {
                    new BadgeSpec(cultivator.RealmText, Palette.Realm(cultivator.Realm)),
                    new BadgeSpec(Labels.TalentGrade(cultivator.TalentGrade), Palette.Talent(cultivator.TalentGrade)),
                    new BadgeSpec(Labels.Of(cultivator.Element) + " 영근", Palette.Of(cultivator.Element)),
                    new BadgeSpec(cultivator.LifeNumber + "번째 생", Palette.Of(LogKind.Rebirth)),
                },
                Link = SheetLink.ToCultivator(cultivator.Id),
            };
        }

        /// <summary>사건 기록 줄.</summary>
        /// <param name="world">세계.</param>
        /// <param name="entry">기록.</param>
        /// <returns>줄 내용.</returns>
        internal static RowModel Log(World world, LogEntry entry)
        {
            bool followed = world.IsFollowed(entry.SubjectId) || world.IsFollowed(entry.OtherId);
            return new RowModel
            {
                Title = entry.Text,
                TitleColor = Palette.Ink,
                Accent = Palette.Of(entry.Kind),
                Badges = followed
                    ? new[] { Kind(entry.Kind), new BadgeSpec("관심", Palette.Gold) }
                    : new[] { Kind(entry.Kind) },
                Value = Fmt.Years(entry.Year),
                ValueColor = Palette.Muted,
                Link = SheetLink.ToCultivator(entry.SubjectId),
            };
        }

        /// <summary>연대기 줄.</summary>
        /// <param name="entry">연대기 항목.</param>
        /// <returns>줄 내용.</returns>
        internal static RowModel Chronicle(ChronicleEntry entry)
        {
            return new RowModel
            {
                Title = entry.Text,
                Accent = Palette.Of(entry.Kind),
                Badges = new[] { new BadgeSpec(Labels.Of(entry.Kind), Palette.Of(entry.Kind)) },
                Value = Fmt.Years(entry.Year),
                ValueColor = Palette.Muted,
                Link = SheetLink.ToCultivator(entry.SubjectId),
            };
        }

        private static BadgeSpec Kind(LogKind kind)
        {
            return new BadgeSpec(Labels.Of(kind), Palette.Of(kind));
        }
    }
}
