﻿using System;

namespace TrueBloodAlly3Bot {
    class Program {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            var theBot = new Bot();
            theBot.RunAsync().GetAwaiter().GetResult();
        }
    }
}
