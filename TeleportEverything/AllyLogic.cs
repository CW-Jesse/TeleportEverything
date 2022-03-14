namespace TeleportEverything
{
    internal partial class Plugin
    {
         public static bool IsEligibleAlly(Character c)
        {
            if (c.m_name.ToLower().Contains("wolf") && TransportWolves.Value)
            {
                return true;
            }

            if (c.name.ToLower().Contains("boar") && TransportBoar.Value)
            {
                return true;
            }

            if (c.name.ToLower().Contains("lox") && TransportLox.Value)
            {
                return true;
            }

            if (IsInMask(c) && TransportMask.Value != "")
            {
                return true;
            }

            return false;
        }

        private static bool IsInMask(Character c)
        {
            var includeList = TransportMask.Value.Split(',');
            foreach (var s in includeList)
            {
                if (c.m_name.ToLower().Contains(s.ToLower().Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsAllyTransportable(Character ally)
        {
            if (IsNamed(ally) && ExcludeNamed)
            {
                return false;
            }

            if (IsFollow(ally) && IncludeFollow)
            {
                return true;
            }

            if (IsNamed(ally) && IncludeNamed)
            {
                return true;
            }

            if (ally.IsTamed() && IncludeTamed)
            {
                return true;
            }

            return false;
        }


        public static bool IsNamed(Character t)
        {
            var name = t.GetComponent<Tameable>()?.GetText();

            return !string.IsNullOrEmpty(name);
        }

        public static bool IsFollow(Character f)
        {
            var mAi = f.GetComponent<MonsterAI>();

            if (mAi != null && mAi.GetFollowTarget() != null &&
                mAi.GetFollowTarget().Equals(Player.m_localPlayer.gameObject))
            {
                return true;
            }

            return false;
        }

        public static void SetFollow(Character f)
        {
            var mAi = f.GetComponent<MonsterAI>();


            mAi.SetFollowTarget(Player.m_localPlayer.gameObject);
        }
        
    }
}