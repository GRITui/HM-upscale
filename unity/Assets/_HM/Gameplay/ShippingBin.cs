using System.Collections.Generic;
using UnityEngine;

namespace HM.Gameplay
{
    public sealed class ShippingBin : Interactable
    {
        public override string Prompt => "E: Ship harvest";
        public override void Interact(PlayerInteractor who) => FarmWorld.Instance?.ShipAll();
    }
}
