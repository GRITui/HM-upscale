using System.Collections.Generic;
using System.IO;
using HM.Dialogue;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests.Farming
{
    public class DialogueTests
    {
        static DialogueRunner Make(HashSet<string> flags = null) =>
            new DialogueRunner(DialogueData.FromJson(File.ReadAllText(
                Path.Combine(Application.dataPath, "_HM/Dialogue/Data/dialogue.json"))), flags);

        [Test] public void StartsAtGreetingWithSpeakerAndFace()
        {
            var r = Make(); r.Begin();
            Assert.AreEqual("Mira", r.Current.speaker);
            Assert.AreEqual(1, r.Current.face);
            Assert.IsTrue(r.HasChoices);
        }

        [Test] public void RequireFlagHidesChoice()
        {
            var r = Make(); r.Begin();
            Assert.AreEqual(2, r.VisibleChoices().Count);
            var r2 = Make(new HashSet<string> { "planted_today" }); r2.Begin();
            Assert.AreEqual(3, r2.VisibleChoices().Count);
        }

        [Test] public void AdviceSetsFlagAndBranchesOnIt()
        {
            var r = Make(); r.Begin();
            r.Choose(0);
            Assert.AreEqual("mira_advice", r.Current.id);
            Assert.IsTrue(r.Flags.Contains("got_advice"));
            r.Advance();                          // mira_check: ifFlag -> mira_bye
            Assert.AreEqual("mira_check", r.Current.id);
            r.Advance();
            Assert.AreEqual("mira_bye", r.Current.id);
            r.Advance();
            Assert.IsTrue(r.Finished);
        }

        [Test] public void ChoiceWithEmptyNextEnds()
        {
            var r = Make(); r.Begin();
            r.Choose(1);
            Assert.IsTrue(r.Finished);
        }

        [Test] public void AdvanceWithPendingChoicesThrows()
        {
            var r = Make(); r.Begin();
            Assert.Throws<System.InvalidOperationException>(() => r.Advance());
        }

        [Test] public void UnknownNodeThrows()
        {
            var r = Make();
            Assert.Throws<KeyNotFoundException>(() => r.Begin("nope"));
        }
    }
}
