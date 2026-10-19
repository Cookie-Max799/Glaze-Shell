using System;
using System.Collections.Generic;
using System.Linq;
using GlazeShell.Core.Glance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlazeShell.Core.Tests;

[TestClass]
public sealed class GlanceRankerTests
{
    private static GlanceResult Result(
        string title,
        GlanceResultKind kind = GlanceResultKind.Application,
        string subtitle = "",
        string keywords = "") =>
        new(kind, title, title, subtitle, keywords);

    [TestMethod]
    public void EmptyQueryReturnsNothing()
    {
        var results = GlanceRanker.Rank([Result("Notepad"), Result("Calculator")], "   ");

        Assert.IsEmpty(results, "Glance must not list everything before the user types.");
    }

    [TestMethod]
    public void NonMatchingCandidatesAreExcluded()
    {
        var results = GlanceRanker.Rank([Result("Notepad"), Result("Calculator")], "note");

        Assert.HasCount(1, results);
        Assert.AreEqual("Notepad", results[0].Title);
    }

    [TestMethod]
    public void ExactTitleRanksBeforePrefix()
    {
        var results = GlanceRanker.Rank(
            [Result("Note"), Result("Notepad"), Result("Note taking")],
            "note");

        Assert.AreEqual("Note", results[0].Title, "An exact match must win over prefix and contains.");
    }

    [TestMethod]
    public void PrefixRanksBeforeContainedMatch()
    {
        var results = GlanceRanker.Rank([Result("Take note"), Result("Notepad")], "note");

        Assert.AreEqual("Notepad", results[0].Title);
        Assert.AreEqual("Take note", results[1].Title);
    }

    [TestMethod]
    public void WordStartRanksBeforeMidWordMatch()
    {
        var results = GlanceRanker.Rank([Result("Panotepad"), Result("Note pad")], "note");

        Assert.AreEqual("Note pad", results[0].Title);
    }

    [TestMethod]
    public void SubtitleMatchIsIncludedButWeaker()
    {
        var results = GlanceRanker.Rank(
            [Result("Player", subtitle: "Windows Media"), Result("Minesweeper")],
            "media");

        Assert.HasCount(1, results);
        Assert.AreEqual("Player", results[0].Title);
    }

    [TestMethod]
    public void KeywordMatchIsIncludedButWeakest()
    {
        var results = GlanceRanker.Rank([Result("Gadget", keywords: "widget")], "widget");

        Assert.HasCount(1, results);
        Assert.AreEqual("Gadget", results[0].Title);
    }

    [TestMethod]
    public void AllTermsMustMatch()
    {
        var results = GlanceRanker.Rank(
            [Result("Visual Studio Code"), Result("Visual Studio")],
            "visual code");

        Assert.HasCount(1, results);
        Assert.AreEqual("Visual Studio Code", results[0].Title);
    }

    [TestMethod]
    public void MatchingIsCaseInsensitive()
    {
        var results = GlanceRanker.Rank([Result("Notepad")], "NOTE");

        Assert.HasCount(1, results);
    }

    [TestMethod]
    public void LimitIsRespected()
    {
        var candidates = Enumerable.Range(0, 30).Select(index => Result($"App {index:00}")).ToArray();

        var results = GlanceRanker.Rank(candidates, "app", limit: 5);

        Assert.HasCount(5, results);
    }

    [TestMethod]
    public void NonPositiveLimitReturnsNothing()
    {
        var results = GlanceRanker.Rank([Result("Notepad")], "note", limit: 0);

        Assert.IsEmpty(results);
    }

    [TestMethod]
    public void EqualScoresAreOrderedByTitle()
    {
        var results = GlanceRanker.Rank([Result("Beta tool"), Result("Alpha tool")], "tool");

        Assert.HasCount(2, results);
        Assert.AreEqual("Alpha tool", results[0].Title);
        Assert.AreEqual("Beta tool", results[1].Title);
    }

    [TestMethod]
    public void NullCollectionsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GlanceRanker.Rank(null!, "note"));
    }

    [TestMethod]
    public void SubtitleAndKeywordsNeverNull()
    {
        var result = new GlanceResult(GlanceResultKind.Setting, "ms-settings:display", "Дисплей", null!, null!);

        Assert.AreEqual(string.Empty, result.Subtitle);
        Assert.AreEqual(string.Empty, result.Keywords);
    }

    [TestMethod]
    public void KindIsPreserved()
    {
        var candidates = new List<GlanceResult>
        {
            new(GlanceResultKind.Setting, "ms-settings:sound", "Sound"),
            new(GlanceResultKind.Command, "open-shell", "Open Glaze Shell"),
        };

        var settings = GlanceRanker.Rank(candidates, "sound");
        Assert.AreEqual(GlanceResultKind.Setting, settings[0].Kind);

        var commands = GlanceRanker.Rank(candidates, "shell");
        Assert.AreEqual(GlanceResultKind.Command, commands[0].Kind);
    }
}
