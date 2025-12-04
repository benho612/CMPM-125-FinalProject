using UnityEngine;
using System.Collections.Generic;

public class IngredientInventory : MonoBehaviour
{
    // What each player owns
    private Dictionary<string, int> counts = new Dictionary<string, int>();

    // Required amounts for the scene
    public Dictionary<string, int> required = new Dictionary<string, int>
    {
        { "Water", 2 },
        { "Dough", 1 },
        { "Noodles", 1 },
        { "Horn", 1 },
        { "Bone", 1 },
        { "Garlic", 1 },
        { "Condiments", 1 },
        { "Soup", 1 },
        { "Pork", 1 },
        { "Egg", 1 },
        { "Vegetable", 1 },
        { "Chopped Vegetables", 1 },
        { "FinalRamenHalf", 1 }
    };

    public void Add(string item, int amount = 1)
    {
        if (!counts.ContainsKey(item))
            counts[item] = 0;

        counts[item] += amount;
    }

    public int GetCount(string item)
    {
        if (!counts.ContainsKey(item))
            return 0;
        return counts[item];
    }

    public int GetRequired(string item)
    {
        return required.ContainsKey(item) ? required[item] : 0;
    }

    public bool HasItem(string item)
    {
        return GetCount(item) >= GetRequired(item);
    }

    public Dictionary<string, int> GetAllCounts()
    {
        return counts;
    }

    public bool HasAllForAssemble()
    {
        return GetCount("Noodles") >= 1 &&
            GetCount("Soup") >= 1 &&
            GetCount("Chopped Vegetables") >= 1;
    }

}
