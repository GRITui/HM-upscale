using HM.Characters;
using HM.Farming;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HM.Gameplay
{
    /// <summary>Tool selection, tool use on the plot in front of the player, and interact key handling.</summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public float reach = 1.0f;
        PlayerMover mover;
        Transform toolAnchor;
        GameObject toolVisual;
        Tool shownTool = (Tool)(-1);

        public Interactable Focus { get; private set; }

        void Awake() => mover = GetComponent<PlayerMover>();

        /// <summary>World point where the held tool lands.</summary>
        public Vector3 Target => transform.position + transform.forward * reach;

        void Update()
        {
            var world = FarmWorld.Instance;
            if (world == null) return;
            var kb = Keyboard.current;

            var npc = NpcDialogue.Active;
            var dir = FarmEventDirector.Instance;
            var bridge = dir != null ? dir.Bridge : null;
            bool evLocked = dir != null && dir.InputLocked;
            if (mover != null) mover.enabled = npc == null && !evLocked;
            if (npc == null && bridge != null && bridge.Active)
            {
                Focus = null;
                if (kb == null) return;
                if (bridge.Runner.HasChoices)
                {
                    int n = bridge.Runner.VisibleChoices().Count;
                    for (int i = 0; i < n && i < 9; i++)
                        if (kb[Key.Digit1 + i].wasPressedThisFrame) { bridge.Choose(i); return; }
                }
                else if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) bridge.Advance();
                return;
            }
            if (evLocked && npc == null) { Focus = null; return; }
            if (npc != null)
            {
                Focus = null;
                if (kb == null) return;
                for (int i = 0; i < 9; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame) { npc.Select(i); return; }
                if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) npc.Select(0);
                if (kb.escapeKey.wasPressedThisFrame) npc.Close();
                return;
            }

            Focus = Interactable.Nearest(transform.position);
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) world.SelectTool(Tool.Hoe);
            if (kb.digit2Key.wasPressedThisFrame) world.SelectTool(Tool.WateringCan);
            if (kb.digit3Key.wasPressedThisFrame) world.SelectTool(Tool.SeedBag);
            if (kb.digit4Key.wasPressedThisFrame) world.SelectTool(Tool.Sickle);
            if (kb.tabKey.wasPressedThisFrame) world.CycleCrop();
            if (kb.bKey.wasPressedThisFrame) world.BuySeed();
            if (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Act();

            ShowTool(world);
        }

        /// <summary>The interact key: nearest interactable first, otherwise use the held tool in front.</summary>
        public void Act()
        {
            var world = FarmWorld.Instance;
            if (world == null) return;
            var i = Interactable.Nearest(transform.position);
            if (i != null && !world.TryGetTile(Target, out _)) { i.Interact(this); return; }
            world.UseHeldAt(Target);
        }

        void ShowTool(FarmWorld world)
        {
            if (shownTool == world.HeldTool) return;
            shownTool = world.HeldTool;
            if (toolVisual != null) Destroy(toolVisual);
            var prefab = world.toolPrefabs != null && (int)shownTool < world.toolPrefabs.Length ? world.toolPrefabs[(int)shownTool] : null;
            if (prefab == null) return;
            if (toolAnchor == null) toolAnchor = FindChildDeep(transform, "arm_r") ?? transform;
            toolVisual = Instantiate(prefab, toolAnchor);
            toolVisual.transform.localPosition = new Vector3(0f, -0.2f, 0.2f);
        }

        static Transform FindChildDeep(Transform t, string name)
        {
            foreach (Transform c in t)
            {
                if (c.name == name) return c;
                var r = FindChildDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
