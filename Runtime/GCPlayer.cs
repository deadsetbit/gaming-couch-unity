using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    public class GCPlayer : MonoBehaviour, IGCPlayer
    {
        private int playerId = -1;
        private string playerName;
        private Color color;

        /// <summary>
        /// Do not call this in your game script. This is called by the GamingCouch script.
        /// If you want to do something custom with the options on player setup, override this method.
        /// NOTE: Don't forget to call base.GamingCouchSetup(options) when overriding.
        /// </summary>
        /// <param name="options">Options provided by the platform </param>
        public virtual void GamingCouchSetup(GCPlayerSetupOptions options)
        {
            playerId = options.playerId;
            playerName = options.name;
            color = options.color;
        }

        /// <summary>
        /// Get the player id provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual int GetId()
        {
            if (playerId == -1)
            {
                throw new System.Exception("Player id is not set. Wait for GamingCouchSetup to be called first. If you are overriding GamingCouchSetup, make sure to call base.GamingCouchSetup(options) in your override.");
            }

            return playerId;
        }

        /// <summary>
        /// Get the player name provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual string GetName()
        {
            return playerName;
        }

        /// <summary>
        /// Get the player color provided by GamingCouch in GCPlayer.GamingCouchSetup.
        /// </summary>
        public virtual Color GetColor()
        {
            return color;
        }
    }
}
