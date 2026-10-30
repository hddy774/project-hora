namespace Chronicles.Game;

public sealed record ItemDefinition(string Id, string Name, ItemKind Kind, int Weight, int Capacity, int EquipmentSlot, string Use);
public sealed record Recipe(string Id, string Name, string Skill, string Output, int Quantity, KeyValuePair<string, int>[] Inputs);
public sealed record PathDefinition(string Id, Realm Realm, string Name, string Book, int[] CombatFactors, int SupplyCost, int Risk);

public static class Catalog
{
    public static IReadOnlyList<string> RealmNames { get; } = Array.AsReadOnly(
        new[] { "범인", "연기경", "축기경", "금단경", "원영경", "화신경", "연허경", "합체경", "도겁경", "대승경", "반선경" });
    public static IReadOnlyList<string> QualityNames { get; } = Array.AsReadOnly(
        new[] { "폐품", "최하품", "하품", "중품", "상품", "최상품", "무결품" });
    public static string Quality(int value) => QualityNames[value switch
        { < 1000 => 0, < 3000 => 1, < 4000 => 2, < 6000 => 3, < 8000 => 4, < 10000 => 5, _ => 6 }];
    public static IReadOnlyList<string> Talents { get; } = Array.AsReadOnly(new[]
    {
        "금행 감응", "목행 감응", "수행 감응", "화행 감응", "토행 감응", "오행 조화", "토납·감응", "운기·경맥",
        "정련·체화", "연체·무도", "신식·탐지", "오도·추론", "법술 운용", "전투 판단", "연단", "연기(煉器)",
        "제부", "진법", "영농·본초", "어수·생태", "의술·회복", "감정·문헌", "상재·교섭", "경세·통솔"
    });
    public static IReadOnlyList<string> PersonalityAxes { get; } = Array.AsReadOnly(new[]
    { "생존", "구도", "가족", "호기심", "협력", "신뢰", "위험", "야심", "인내", "명예", "자율", "규율", "공감", "경쟁" });
    public static IReadOnlyList<string> TraitNames { get; } = Array.AsReadOnly(new[]
    {
        "통맥영체", "후토영체", "한맥지구", "영기편성", "고맥잠장", "혈맥공명", "이맥교융", "혈맥상충",
        "숙혜잔광", "혼기견고", "윤회유흔", "혼상미유", "침정여수", "강직불굴", "호기무궁", "쟁승호투",
        "장생일념", "구도불권", "수호지심", "중낙수신", "일과불철", "신행원려", "임기응변", "적곡비황",
        "사은미보", "혈맹지서", "구원미해", "세교지연", "길운상조", "액운미산", "봉흉유로", "기연상인",
        "경맥구상", "도기불온", "오행실형", "금단유하", "창종지조", "개국지군", "봉지지주", "유망종실"
    });
    public static IReadOnlyList<string> PolicyNames { get; } = Array.AsReadOnly(new[]
    { "입문·선발", "교육·전승", "수행·자원", "기예·산업", "경제·분배", "규율·상벌", "대외·외교", "영토·방위", "민생·구휼", "승계·인사" });
    public static IReadOnlyList<string> EquipmentSlots { get; } = Array.AsReadOnly(new[]
    { "무기", "보조 법보", "의복", "갑주", "머리", "신발", "장신구", "호신", "도구", "단로", "진기", "저물대" });

    public static IReadOnlyList<PathDefinition> Paths { get; } = BuildPaths();
    public static IReadOnlyDictionary<string, ItemDefinition> Items { get; } = BuildItems();
    public static IReadOnlyList<Recipe> Recipes { get; } = Array.AsReadOnly<Recipe>(
    [
        new("pill", "회춘단 제조", "alchemy", "pill", 1, [new("herb", 3), new("wood", 1)]),
        new("weapon", "현철검 제작", "smith", "weapon", 1, [new("ore", 4), new("wood", 1)]),
        new("tool", "채집 도구 제작", "smith", "tool", 1, [new("ore", 2), new("wood", 2)]),
        new("talisman", "호신부 제작", "talisman", "talisman", 1, [new("paper", 2), new("ink", 1)]),
        new("array", "수행 진기 제작", "array", "array", 1, [new("ore", 3), new("wood", 3)]),
        new("paper", "부지 제조", "farm", "paper", 3, [new("wood", 2)]),
        new("ink", "영묵 제조", "alchemy", "ink", 2, [new("herb", 1), new("ore", 1)]),
        new("bag", "저물대 제작", "smith", "bag", 1, [new("cloth", 4), new("ore", 2)]),
        new("cloth", "영포 직조", "farm", "cloth", 2, [new("herb", 3)]),
        new("armor", "호신갑 제작", "smith", "armor", 1, [new("ore", 4), new("cloth", 2)])
    ]);

    private static System.Collections.ObjectModel.ReadOnlyCollection<PathDefinition> BuildPaths()
    {
        string[][] names =
        [
            ["감응 토납", "영근별 분류 운기", "정탁·체화"], ["인도", "지도", "천도"],
            ["규모 확장", "정련·강화", "순환 연계"], ["정양 보전", "정밀 제어", "경맥 협응"],
            ["집중 조율", "역할 분담", "동조 운용"], ["결함 정화", "경로 재편", "오행 재조화"],
            ["동시 합류", "완충 합류", "상생 합류"], ["내부 지탱", "외부 연동", "협응 응변"],
            ["공법 회통", "전체 원융", "전이 적응"], ["자립 온양", "계역 적응", "통로 협응"]
        ];
        string[][] books =
        [
            ["청허감응편", "오행행기요결", "정탁토납록"], ["삼재축기도해", "현기안로결", "도기회환편"],
            ["황정결단법", "구전정단경", "오기환단록"], ["파단성영비록", "영태정양경", "원영귀맥결"],
            ["신영장성편", "분령양신결", "합조신식록"], ["허기세근경", "정허환본결", "오행공명록"],
            ["형신합류편", "동원귀일결", "합체조화록"], ["응겁회로경", "겁후정맥편", "도심문겁록"],
            ["만상원융경", "도법회통록", "승선정기편"], ["반선온도경", "천문감응편", "비승정원록"]
        ];
        var result = new List<PathDefinition>();
        for (var r = 0; r < 10; r++)
            for (var n = 0; n < 3; n++)
            {
                int[][] factors = [[120, 80, 100, 90, 110, 80, 100, 90],
                    [85, 120, 90, 110, 80, 110, 100, 100], [100, 95, 115, 90, 100, 105, 85, 110]];
                result.Add(new($"path.{r + 1}.{n}", (Realm)(r + 1), names[r][n], books[r][n], factors[n], n + 1, n * 100));
            }
        return result.AsReadOnly();
    }

    private static System.Collections.ObjectModel.ReadOnlyDictionary<string, ItemDefinition> BuildItems()
    {
        var result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        void Add(string id, string name, ItemKind kind, int slot = -1, int capacity = 0, string use = "재료") =>
            result.Add(id, new(id, name, kind, 1, capacity, slot, use));
        Add("food", "영곡", ItemKind.Food, use: "식량"); Add("herb", "청령초", ItemKind.Herb);
        Add("ore", "적철광", ItemKind.Ore); Add("wood", "청심목", ItemKind.Wood);
        Add("cloth", "영포", ItemKind.Cloth); Add("paper", "영지", ItemKind.Paper); Add("ink", "영묵", ItemKind.Ink);
        Add("pill", "회춘단", ItemKind.Pill, use: "건강 회복"); Add("tool", "채집 도구", ItemKind.Tool, 8);
        Add("talisman", "호신부", ItemKind.Talisman, use: "한 번의 방어"); Add("array", "수행 진기", ItemKind.Array, 10);
        Add("bag", "저물대", ItemKind.Bag, 11, 200); Add("weapon", "현철검", ItemKind.Weapon, 0);
        Add("armor", "호신갑", ItemKind.Armor, 3);
        foreach (var path in Paths) Add(path.Id, path.Book, ItemKind.Book, use: "공법 학습");
        for (var i = 0; i < EquipmentSlots.Count; i++)
            Add($"equipment.{i}", EquipmentSlots[i], i == 11 ? ItemKind.Bag : ItemKind.Tool, i, i == 11 ? 100 : 0);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, ItemDefinition>(result);
    }

    public static int Talent(Person p, int index)
    {
        if ((uint)index >= Talents.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (index < 5) return p.Roots[index];
        var a = p.Aptitudes;
        return index switch
        {
            5 => (p.Roots.Min() + a[3]) / 2,
            6 => (a[0] + a[4]) / 2, 7 => (a[1] + a[4]) / 2, 8 or 9 => (a[1] + a[2]) / 2,
            10 or 19 => (a[4] + a[5]) / 2, 14 => (a[3] + a[4] + p.Roots[3]) / 3,
            15 => (a[1] + a[3] + p.Roots[0]) / 3, 18 => (a[3] + p.Roots[1] + p.Roots[4]) / 3,
            _ => (a[3] + a[4]) / 2
        };
    }
}
