using How2Shop.Items;
using System.Collections.Generic;
using UnityEngine;

public sealed class GrandmaShoppingRequest
{
    public struct TagWeight
    {
        public readonly string Tag;
        public readonly int Weight;

        public TagWeight(string tag, int weight)
        {
            Tag = tag;
            Weight = weight;
        }
    }

    private readonly TagWeight[] tagWeights;
    private readonly string commonMisreadTag;
    private readonly string commonMisreadFeedback;

    public string Prompt { get; }
    public string IntendedMeaning { get; }
    public string CorrectFeedback { get; }

    public GrandmaShoppingRequest(string prompt, string intendedMeaning, string correctFeedback,
        string commonMisreadTag, string commonMisreadFeedback, params TagWeight[] tagWeights)
    {
        Prompt = prompt;
        IntendedMeaning = intendedMeaning;
        CorrectFeedback = correctFeedback;
        this.commonMisreadTag = commonMisreadTag;
        this.commonMisreadFeedback = commonMisreadFeedback;
        this.tagWeights = tagWeights;
    }

    public int Score(ItemDefinition item)
    {
        if (item == null || tagWeights == null)
            return 0;

        int score = 0;
        for (int i = 0; i < tagWeights.Length; i++)
        {
            if (item.HasTag(tagWeights[i].Tag))
                score += tagWeights[i].Weight;
        }

        return Mathf.Clamp(score, 0, 100);
    }

    public string GetFeedback(ItemDefinition item, int score)
    {
        if (score >= 80)
            return CorrectFeedback;

        if (item != null && !string.IsNullOrEmpty(commonMisreadTag) &&
            item.HasTag(commonMisreadTag))
            return commonMisreadFeedback;

        return "Not quite, love. Nan meant " + IntendedMeaning + ".";
    }

    public bool IsCommonMisread(ItemDefinition item)
    {
        return item != null && !string.IsNullOrEmpty(commonMisreadTag) && item.HasTag(commonMisreadTag);
    }
}

public static class GrandmaShoppingRequests
{
    private static readonly GrandmaShoppingRequest[] requests =
    {
        new GrandmaShoppingRequest(
            "Something light,\nbut not too light.",
            "light in calories",
            "Spot on! Nan meant light in calories.",
            "sunshine",
            "You bought actual sunshine. Nan wanted something light in calories!",
            new GrandmaShoppingRequest.TagWeight("diet", 45),
            new GrandmaShoppingRequest.TagWeight("low-calorie", 25),
            new GrandmaShoppingRequest.TagWeight("drink", 10),
            new GrandmaShoppingRequest.TagWeight("cola", 10),
            new GrandmaShoppingRequest.TagWeight("light", 10),
            new GrandmaShoppingRequest.TagWeight("sunshine", 25),
            new GrandmaShoppingRequest.TagWeight("lightweight", 10)),
        new GrandmaShoppingRequest(
            "Something for\na proper cup of tea.",
            "tea to drink",
            "Lovely. A proper brew, just as Nan meant.",
            "letter-t",
            "That's the letter T, love. Nan wanted tea to drink!",
            new GrandmaShoppingRequest.TagWeight("tea", 100),
            new GrandmaShoppingRequest.TagWeight("letter-t", 35),
            new GrandmaShoppingRequest.TagWeight("biscuits", 20),
            new GrandmaShoppingRequest.TagWeight("bakery", 10)),
        new GrandmaShoppingRequest(
            "Something that'll\nkeep in the cupboard.",
            "a shelf-stable pantry item",
            "Good thinking. That'll keep nicely in the cupboard.",
            "frozen",
            "That's for the freezer, love. Nan wanted something for the cupboard.",
            new GrandmaShoppingRequest.TagWeight("tinned", 100),
            new GrandmaShoppingRequest.TagWeight("pantry", 50)),
        new GrandmaShoppingRequest(
            "Something fresh,\nnot from a tin.",
            "fresh produce",
            "Fresh as anything. Nan will be pleased.",
            "tinned",
            "You took 'fresh' as a tin label. Nan wanted fresh produce.",
            new GrandmaShoppingRequest.TagWeight("fresh", 60),
            new GrandmaShoppingRequest.TagWeight("produce", 85),
            new GrandmaShoppingRequest.TagWeight("fruit", 15),
            new GrandmaShoppingRequest.TagWeight("vegetable", 15)),
        new GrandmaShoppingRequest(
            "Something from\nthe sea, please.",
            "fish or seafood",
            "Straight from the sea. That's exactly it.",
            "toy-submarine",
            "A submarine belongs in the sea, but Nan wanted fish for supper!",
            new GrandmaShoppingRequest.TagWeight("fish", 80),
            new GrandmaShoppingRequest.TagWeight("toy-submarine", 35),
            new GrandmaShoppingRequest.TagWeight("seafood", 25),
            new GrandmaShoppingRequest.TagWeight("protein", 10)),
        new GrandmaShoppingRequest(
            "Something to clean\nup the little mess.",
            "a cleaning product",
            "That should get the mess cleaned up nicely.",
            "food",
            "That's food, love. Nan meant something to clean with.",
            new GrandmaShoppingRequest.TagWeight("cleaning", 100),
            new GrandmaShoppingRequest.TagWeight("household", 20),
            new GrandmaShoppingRequest.TagWeight("non-food", 10)),
        new GrandmaShoppingRequest(
            "Something for the toaster.", "bread for toast", "Lovely toast, just as Nan wanted.",
            "bakery", "A bakery treat is nice, but Nan wanted bread for toast.",
            new GrandmaShoppingRequest.TagWeight("bread", 100),
            new GrandmaShoppingRequest.TagWeight("loaf", 80),
            new GrandmaShoppingRequest.TagWeight("bakery", 20)),
        new GrandmaShoppingRequest(
            "Something to put in my tea.", "milk for tea", "A splash of milk for Nan's tea.",
            "tea", "Nan already has the tea; she wanted milk to put in it.",
            new GrandmaShoppingRequest.TagWeight("milk", 90),
            new GrandmaShoppingRequest.TagWeight("dairy", 10),
            new GrandmaShoppingRequest.TagWeight("chocolate", -80)),
        new GrandmaShoppingRequest(
            "Something to lift me up.", "coffee", "That coffee will lift Nan's spirits.",
            "toy-helicopter", "A helicopter would lift Nan up, but she meant a cup of coffee!",
            new GrandmaShoppingRequest.TagWeight("coffee", 100),
            new GrandmaShoppingRequest.TagWeight("toy-helicopter", 35),
            new GrandmaShoppingRequest.TagWeight("tea", 30))
    };

    private static readonly List<GrandmaShoppingRequest> currentList = new List<GrandmaShoppingRequest>();
    private static bool tutorialRequested;
    public static int ItemCount { get; private set; } = 3;
    public static IReadOnlyList<GrandmaShoppingRequest> CurrentList
    {
        get { if (currentList.Count == 0) BeginRound(ItemCount); return currentList; }
    }

    public static GrandmaShoppingRequest Current
    {
        get
        {
            return CurrentList[0];
        }
    }

    public static void BeginRound(int count)
    {
        ItemCount = count == 9 ? 9 : count == 6 ? 6 : 3;
        var pool = new List<GrandmaShoppingRequest>(requests);
        currentList.Clear();
        while (currentList.Count < ItemCount && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            currentList.Add(pool[index]);
            pool.RemoveAt(index);
        }
    }

    public static void RequestGameplayTutorial() { tutorialRequested = true; }

    public static bool ConsumeGameplayTutorial()
    {
        bool requested = tutorialRequested;
        tutorialRequested = false;
        return requested;
    }

    public sealed class BasketResult
    {
        public int Score;
        public ItemDefinition[] MatchedItems;
        public int[] MatchScores;
        public int ExtraItems;
        public List<ItemDefinition> UnmatchedItems = new List<ItemDefinition>();
    }

    // Find the same maximum-scoring one-to-one assignment used for grading,
    // and keep the assignments so the player can see why Nan gave that grade.
    public static BasketResult EvaluateBasket(IReadOnlyList<ItemDefinition> items)
    {
        var list = CurrentList;
        int count = items == null ? 0 : items.Count;
        int states = 1 << list.Count;
        int[,] best = new int[count + 1, states];
        int[,] previous = new int[count + 1, states];
        int[,] selected = new int[count + 1, states];
        for (int i = 0; i <= count; i++)
            for (int m = 0; m < states; m++) { best[i, m] = -1; selected[i, m] = -1; }
        best[0, 0] = 0;
        for (int i = 0; i < count; i++)
        {
            for (int mask = 0; mask < states; mask++)
            {
                if (best[i, mask] < 0) continue;
                if (best[i, mask] > best[i + 1, mask])
                {
                    best[i + 1, mask] = best[i, mask];
                    previous[i + 1, mask] = mask;
                    selected[i + 1, mask] = -1;
                }
                for (int request = 0; request < list.Count; request++)
                {
                    int bit = 1 << request;
                    if ((mask & bit) != 0) continue;
                    int score = list[request].Score(items[i]);
                    int next = mask | bit;
                    if (score > 0 && best[i, mask] + score > best[i + 1, next])
                    {
                        best[i + 1, next] = best[i, mask] + score;
                        previous[i + 1, next] = mask;
                        selected[i + 1, next] = request;
                    }
                }
            }
        }
        int bestMask = 0;
        for (int mask = 1; mask < states; mask++)
            if (best[count, mask] > best[count, bestMask]) bestMask = mask;
        var result = new BasketResult { MatchedItems = new ItemDefinition[list.Count], MatchScores = new int[list.Count] };
        result.Score = Mathf.RoundToInt(best[count, bestMask] / (float)Mathf.Max(1, Mathf.Max(list.Count, count)));
        int matched = 0;
        bool[] used = new bool[count];
        for (int i = count; i > 0; i--)
        {
            int request = selected[i, bestMask];
            if (request >= 0)
            {
                result.MatchedItems[request] = items[i - 1];
                used[i - 1] = true;
                result.MatchScores[request] = list[request].Score(items[i - 1]);
                matched++;
            }
            bestMask = previous[i, bestMask];
        }
        result.ExtraItems = count - matched;
        for (int i = 0; i < count; i++)
            if (!used[i]) result.UnmatchedItems.Add(items[i]);
        return result;
    }

    public static int ScoreBasket(IReadOnlyList<ItemDefinition> items)
    {
        return EvaluateBasket(items).Score;
    }

    public static string FindMisreadFeedback(IReadOnlyList<ItemDefinition> items)
    {
        foreach (var request in CurrentList)
            foreach (var item in items)
                if (request.IsCommonMisread(item) && request.Score(item) < 80)
                    return request.GetFeedback(item, request.Score(item));
        return null;
    }
}
