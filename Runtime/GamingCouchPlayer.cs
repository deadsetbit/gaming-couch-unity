using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class GamingCouchPlayer : MonoBehaviour, IGamingCouchPlayer
    {
        private Color color;
        private int playerId;

        public virtual void GamingCouchSetup(GamingCouchPlayerSetupOptions options)
        {
            playerId = options.playerId;
            name = options.name;
            color = options.color;
        }

        public virtual int GetId()
        {
            return playerId;
        }

        public virtual string GetName()
        {
            return name;
        }

        public virtual Color GetColor()
        {
            return color;
        }
    }
}
