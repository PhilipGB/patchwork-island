using System;

namespace PatchworkIsland;

/// <summary>Pure shopping / budget arithmetic used by the in-game shops.</summary>
public static class BudgetRules
{
    public const int GroceryBudget = 12;
    public const int BreadPrice = 3;
    public const int MilkPrice = 4;
    public const int RequiredBread = 2;
    public const int RequiredMilk = 1;
    public const int GroceryExpectedTotal = 10;
    public const int GroceryExpectedChange = 2;

    public const int ParkBudget = 20;
    public const int BenchPrice = 6;
    public const int FlowerbedPrice = 4;
    public const int LampPrice = 9;

    public const int TablePrice = 8;
    public const int ChairPrice = 5;
    public const int PlantPrice = 3;

    public static int GroceryTotal(int bread, int milk) => bread * BreadPrice + milk * MilkPrice;

    public static int GroceryChange(int bread, int milk) =>
        Math.Max(0, GroceryBudget - GroceryTotal(bread, milk));

    public static int GroceryPrice(string item) => item switch
    {
        "bread" => BreadPrice,
        "milk" => MilkPrice,
        _ => 0,
    };

    public static bool CanAddGrocery(int bread, int milk, string item)
    {
        int price = GroceryPrice(item);
        if (price <= 0) return false;
        return GroceryTotal(bread, milk) + price <= GroceryBudget;
    }

    public static bool IsGroceryListComplete(int bread, int milk) =>
        bread >= RequiredBread && milk >= RequiredMilk;

    public static bool IsGroceryCorrect(int bread, int milk) =>
        bread == RequiredBread && milk == RequiredMilk;

    public static int ParkTotal(int benches, int flowerbeds, int lamps) =>
        benches * BenchPrice + flowerbeds * FlowerbedPrice + lamps * LampPrice;

    public static int ParkPrice(string item) => item switch
    {
        "bench" => BenchPrice,
        "flowerbed" => FlowerbedPrice,
        "lamp" => LampPrice,
        _ => 0,
    };

    public static bool ParkPiecesComplete(int benches, int flowerbeds, int lamps) =>
        benches >= 1 && flowerbeds >= 1 && lamps >= 1;

    public static int FurnitureTotal(int tables, int chairs, int plants) =>
        tables * TablePrice + chairs * ChairPrice + plants * PlantPrice;

    public static int FurniturePrice(string item) => item switch
    {
        "table" => TablePrice,
        "chair" => ChairPrice,
        "plant" => PlantPrice,
        _ => 0,
    };
}
