using System.Collections.Generic;
using UnityEngine;

namespace HM.Gameplay
{
    public sealed class Bed : Interactable
    {
        public override string Prompt => "E: Sleep";
        public override void Interact(PlayerInteractor who) => SleepController.Instance?.Sleep("You slept.");
    }
}
