﻿using ScriptingFramework;

namespace Dialogue
{
    internal sealed class ScriptEvents : IEventProvider
    {
        public void StartDay()
        {
            DayNightCycle.Instance.StartNewDay();
        }
    }
}
