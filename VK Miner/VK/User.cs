using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;

namespace VK_Miner.VK
{
    public class User
    {
        public VK.Model.User Model;

        public int ArrayIndex;
        public Color3 Color;
        public HashSet<int> Friends = new HashSet<int>();
        public HashSet<int> AllFriends = new HashSet<int>();
    }
}
