using UnityEngine;

namespace Orbital.Player
{
    public class PlayerModel
    {
        public const string SelfId = "self";
        private string _id;
        public PlayerModel(string id)
        {
            _id = id;
        }
    }
}
