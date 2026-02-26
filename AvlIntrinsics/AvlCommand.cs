using Jotunn.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvlIntrinsics
{
    public class AvlCommand : ConsoleCommand
    {
        public override string Name => "avl";

        public override string Help => "Avledet server command edifice";

        public override void Run(string[] args)
        {
            if (!(ZNet.instance.IsDedicated() && !ZNet.instance.IsServer()))
            {
                ZPackage pkg = new ZPackage();
                pkg.Write(args.Length);
                foreach (string arg in args)
                {
                    pkg.Write(arg);
                }

                Console.instance.Print("[AvlCommand] command sent (" + args);
                ZNet.instance.GetServerRPC().Invoke("_AvlCommand", pkg);
            }
            else
            {
                Console.instance.Print("[AvlCommand] .avl can only be ran while connected to a server");
            }
        }
    }
}
