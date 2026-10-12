using System;
using System.Collections.Generic;
using UnityEngine;

namespace HM.Dialogue
{
    [Serializable]
    public class ChoiceData
    {
        public string text;
        public string next;         // "" ends the conversation
        public string requireFlag;  // choice hidden unless this flag is set
        public string[] setFlags;
    }

    [Serializable]
    public class NodeData
    {
        public string id;
        public string speaker;
        public int face;
        public string text;
        public string next;         // "" ends the conversation
        public string ifFlag;       // if set, go to ifNext instead of next
        public string ifNext;
        public string[] setFlags;   // applied when the node is entered
        public ChoiceData[] choices;
    }

    [Serializable]
    public class DialogueData
    {
        public int schema;
        public string source;
        public string start;
        public NodeData[] nodes;

        public static DialogueData FromJson(string json)
        {
            var d = JsonUtility.FromJson<DialogueData>(json);
            if (d == null || d.schema != 1 || d.nodes == null) throw new FormatException("dialogue.json: unsupported schema");
            return d;
        }
    }
}
