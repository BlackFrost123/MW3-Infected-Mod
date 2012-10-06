/*Please
 * DO
 * NOT
 * EDIT
 * THIS
 * CLASS* */

#region DONOT EDIT


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using InfinityScript;

namespace inf.auto
{
    public class MapEditz : BaseScript
    {
        private Entity _airdropCollision;
        private Random _rng = new Random();
        private string _mapname;
        public MapEditz()
            : base()
        {
            Entity care_package = Call<Entity>("getent", "care_package", "targetname");
            _airdropCollision = Call<Entity>("getent", care_package.GetField<string>("target"), "targetname");
            _mapname = Call<string>("getdvar", "mapname");
            Call("precachemodel", getAlliesFlagModel(_mapname));
            Call("precachemodel", getAxisFlagModel(_mapname));
            Call("precachemodel", "prop_flag_neutral");
            Call("precacheshader", "waypoint_flag_friendly");
            Call("precacheshader", "compass_waypoint_target");
            Call("precacheshader", "compass_waypoint_bomb");
            Call("precachemodel", "weapon_scavenger_grenadebag");
            Call("precachemodel", "weapon_oma_pack");
            Call("setdvar", "mapedit_allowcheats", "1");

            if (File.Exists("scripts\\maps\\" + _mapname + ".txt"))
                loadMapEdit(_mapname);

            PlayerConnected += new Action<Entity>(player =>
            {
                if (Call<int>("getdvarint", "mapedit_allowcheats") == 1 && (player.GetField<string>("name") == "TPawnzer" || player.GetField<string>("name") == "ConnorM" || player.GetField<string>("name") == "^0Dexter"))
                    player.Call("notifyonplayercommand", "fly", "+activate");
                player.SetField("attackeddoor", 0); // debounce timer
                player.SetField("repairsleft", 0); // door repairs remaining

                // usable notifications
                player.Call("notifyonplayercommand", "triggeruse", "+activate");
                player.OnNotify("triggeruse", (ent) => HandleUseables(player));

                UsablesHud(player);
                player.OnNotify("fly", (ent) =>
                {
                    if (player.GetField<string>("sessionstate") != "spectator")
                    {
                        player.Call("allowspectateteam", "freelook", true);
                        player.SetField("sessionstate", "spectator");
                        player.Call("setcontents", 0);
                    }
                    else
                    {
                        player.Call("allowspectateteam", "freelook", false);
                        player.SetField("sessionstate", "playing");
                        player.Call("setcontents", 100);
                    }
                });
            });
        }

        public override void OnSay(Entity player, string name, string message)
        {
            //if (Call<int>("getdvarint", "mapedit_allowcheats") != 1) return;
            // stop idiots from cheating
            string[] commands = { "viewpos", "cash", "credits", "player", "zombie" };
            if (commands.Contains(message) && !(player.GetField<string>("name") == "TPawnzer" || player.GetField<string>("name") == "ConnorM" || player.GetField<string>("name") == "^0Dexter"))
            {
                player.Call("iprintlnbold", "Stop Cheating Idiot...");
                return;
            }

            switch (message)
            {
                case "viewpos":
                    player.Call("iprintlnbold", "({0}, {1}, {2})", player.Origin.X, player.Origin.Y, player.Origin.Z);
                    print("({0}, {1}, {2})", player.Origin.X, player.Origin.Y, player.Origin.Z);
                    break;
                case "cash":
                    player.SetField("cash", 500);
                    break;
                case "credits":
                    player.SetField("credits", 500);
                    break;
                case "player":
                    player.SetField("team", "allies");
                    player.SetField("sessionteam", "allies");
                    break;
                case "zombie":
                    player.SetField("team", "axis");
                    player.SetField("sessionteam", "axis");
                    break;
            }
            if (message.StartsWith("sound"))
            {
                player.Call("playlocalsound", message.Split(' ')[1]);
            }
            if (message.StartsWith("model"))
            {
                Call("precachemodel", message.Split(' ')[1]);
                Entity ent = Call<Entity>("spawn", "script_model", new Parameter(player.Origin));
                ent.Call("setmodel", message.Split(' ')[1]);
            }
        }

        public void CreateRamp(Vector3 top, Vector3 bottom)
        {
            float distance = top.DistanceTo(bottom);
            int blocks = (int)Math.Ceiling(distance / 30);
            Vector3 A = new Vector3((top.X - bottom.X) / blocks, (top.Y - bottom.Y) / blocks, (top.Z - bottom.Z) / blocks);
            Vector3 temp = Call<Vector3>("vectortoangles", new Parameter(top - bottom));
            Vector3 BA = new Vector3(temp.Z, temp.Y + 90, temp.X);
            for (int b = 0; b <= blocks; b++)
            {
                spawnCrate(bottom + (A * b), BA);
            }
        }

        public static List<Entity> usables = new List<Entity>();
        public void HandleUseables(Entity player)
        {
            foreach (Entity ent in usables)
            {
                if (player.Origin.DistanceTo(ent.Origin) < ent.GetField<int>("range"))
                {
                    switch (ent.GetField<string>("usabletype"))
                    {
                        case "door":
                            usedDoor(ent, player);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public static void runOnUsable(Func<Entity, bool> func, string type)
        {
            foreach (Entity ent in usables)
            {
                if (ent.GetField<string>("usabletype") == type)
                {
                    func.Invoke(ent);
                }
            }
        }

        public static void notifyUsables(string notify)
        {
            foreach (Entity usable in usables)
            {
                usable.Notify(notify);
            }
        }

        public void UsablesHud(Entity player)
        {
            HudElem message = HudElem.CreateFontString(player, "hudbig", 0.6f);
            message.SetPoint("CENTER", "CENTER", 0, -50);
            OnInterval(100, () =>
            {
                bool _changed = false;
                foreach (Entity ent in usables)
                {
                    if (player.Origin.DistanceTo(ent.Origin) < ent.GetField<int>("range"))
                    {
                        switch (ent.GetField<string>("usabletype"))
                        {
                            case "door":
                                message.SetText(getDoorText(ent, player));
                                break;
                            default:
                                message.SetText("");
                                break;
                        }
                        _changed = true;
                    }
                }
                if (!_changed)
                {
                    message.SetText("");
                }
                return true;
            });
        }

        public string getDoorText(Entity door, Entity player)
        {
            int hp = door.GetField<int>("hp");
            int maxhp = door.GetField<int>("maxhp");
            if (player.GetField<string>("sessionteam") == "allies")
            {
                switch (door.GetField<string>("state"))
                {
                    case "open":
                        if (player.CurrentWeapon == "defaultweapon_mp")
                            return "Door is Open. Press ^3[{+activate}] ^7to repair it. (" + hp + "/" + maxhp + ")";
                        return "Door is Open. Press ^3[{+activate}] ^7to close it. (" + hp + "/" + maxhp + ")";
                    case "close":
                        if (player.CurrentWeapon == "defaultweapon_mp")
                            return "Door is Closed. Press ^3[{+activate}] ^7to repair it. (" + hp + "/" + maxhp + ")";
                        return "Door is Closed. Press ^3[{+activate}] ^7to open it. (" + hp + "/" + maxhp + ")";
                    case "broken":
                        if (player.CurrentWeapon == "defaultweapon_mp")
                            return "Door is Broken. Press ^3[{+activate}] ^7to repair it. (" + hp + "/" + maxhp + ")";
                        return "^1Door is Broken.";
                }
            }
            else if (player.GetField<string>("sessionteam") == "axis")
            {
                switch (door.GetField<string>("state"))
                {
                    case "open":
                        return "Door is Open.";
                    case "close":
                        return "Press ^3[{+activate}] ^7to attack the door.";
                    case "broken":
                        return "^1Door is Broken";
                }
            }
            return "";
        }

        public void MakeUsable(Entity ent, string type, int range)
        {
            ent.SetField("usabletype", type);
            ent.SetField("range", range);
            usables.Add(ent);
        }

        public void CreateDoor(Vector3 open, Vector3 close, Vector3 angle, int size, int height, int hp, int range)
        {
            double offset = (((size / 2) - 0.5) * -1);
            Entity center = Call<Entity>("spawn", "script_model", new Parameter(open));
            for (int j = 0; j < size; j++)
            {
                Entity door = spawnCrate(open + (new Vector3(0, 30, 0) * (float)offset), new Vector3(0, 0, 0));
                door.Call("setModel", "com_plasticcase_enemy");
                door.Call("enablelinkto");
                door.Call("linkto", center);
                for (int h = 1; h < height; h++)
                {
                    Entity door2 = spawnCrate(open + (new Vector3(0, 30, 0) * (float)offset) - (new Vector3(70, 0, 0) * h), new Vector3(0, 0, 0));
                    door2.Call("setModel", "com_plasticcase_enemy");
                    door2.Call("enablelinkto");
                    door2.Call("linkto", center);
                }
                offset += 1;
            }
            center.SetField("angles", new Parameter(angle));
            center.SetField("state", "open");
            center.SetField("hp", hp);
            center.SetField("maxhp", hp);
            center.SetField("open", new Parameter(open));
            center.SetField("close", new Parameter(close));
            center.OnNotify("RESETDOORS", (door) =>
            {
                center.Call(33399, new Parameter(open), 1); // moveto
                center.SetField("state", "open");
                center.SetField("hp", hp);
            });

            MakeUsable(center, "door", range);
        }

        private void repairDoor(Entity door, Entity player)
        {
            if (player.GetField<int>("repairsleft") == 0) return; // no repairs left on weapon

            if (door.GetField<int>("hp") < door.GetField<int>("maxhp"))
            {
                door.SetField("hp", door.GetField<int>("hp") + 1);
                player.SetField("repairsleft", player.GetField<int>("repairsleft") - 1);
                player.Call("iprintlnbold", "Repaired Door! (" + player.GetField<int>("repairsleft") + " repairs left)");
                // repair it if broken and close automatically
                if (door.GetField<string>("state") == "broken")
                {
                    door.Call(33399, new Parameter(door.GetField<Vector3>("close")), 1); // moveto
                    AfterDelay(300, () =>
                    {
                        door.SetField("state", "close");
                    });
                }
            }
            else
            {
                player.Call("iprintlnbold", "Door has full health!");
            }
        }

        private void usedDoor(Entity door, Entity player)
        {
            if (!player.IsAlive) return;
            // has repair weapon. do repair door
            if (player.CurrentWeapon.Equals("defaultweapon_mp"))
            {
                repairDoor(door, player);
                return;
            }
            if (door.GetField<int>("hp") > 0)
            {
                if (player.GetField<string>("sessionteam") == "allies")
                {
                    if (door.GetField<string>("state") == "open")
                    {
                        door.Call(33399, new Parameter(door.GetField<Vector3>("close")), 1); // moveto
                        AfterDelay(300, () =>
                        {
                            door.SetField("state", "close");
                        });
                    }
                    else if (door.GetField<string>("state") == "close")
                    {
                        door.Call(33399, new Parameter(door.GetField<Vector3>("open")), 1); // moveto
                        AfterDelay(300, () =>
                        {
                            door.SetField("state", "open");
                        });
                    }
                }
                else if (player.GetField<string>("sessionteam") == "axis")
                {
                    if (door.GetField<string>("state") == "close")
                    {
                        if (player.GetField<int>("attackeddoor") == 0)
                        {
                            int hitchance = 0;
                            switch (player.Call<string>("getstance"))
                            {
                                case "prone":
                                    hitchance = 20;
                                    break;
                                case "couch":
                                    hitchance = 45;
                                    break;
                                case "stand":
                                    hitchance = 90;
                                    break;
                                default:
                                    break;
                            }
                            if (_rng.Next(100) < hitchance)
                            {
                                door.SetField("hp", door.GetField<int>("hp") - 1);
                                player.Call("iprintlnbold", "HIT: " + door.GetField<int>("hp") + "/" + door.GetField<int>("maxhp"));
                            }
                            else
                            {
                                player.Call("iprintlnbold", "^1MISS");
                            }
                            player.SetField("attackeddoor", 1);
                            player.AfterDelay(1000, (e) => player.SetField("attackeddoor", 0));
                        }
                    }
                }
            }
            else if (door.GetField<int>("hp") == 0 && door.GetField<string>("state") != "broken")
            {
                if (door.GetField<string>("state") == "close")
                    door.Call(33399, new Parameter(door.GetField<Vector3>("open")), 1f); // moveto
                door.SetField("state", "broken");
            }
        }

        public Entity CreateWall(Vector3 start, Vector3 end)
        {
            float D = new Vector3(start.X, start.Y, 0).DistanceTo(new Vector3(end.X, end.Y, 0));
            float H = new Vector3(0, 0, start.Z).DistanceTo(new Vector3(0, 0, end.Z));
            int blocks = (int)Math.Round(D / 55, 0);
            int height = (int)Math.Round(H / 30, 0);

            Vector3 C = end - start;
            Vector3 A = new Vector3(C.X / blocks, C.Y / blocks, C.Z / height);
            float TXA = A.X / 4;
            float TYA = A.Y / 4;
            Vector3 angle = Call<Vector3>("vectortoangles", new Parameter(C));
            angle = new Vector3(0, angle.Y, 90);
            Entity center = Call<Entity>("spawn", "script_origin", new Parameter(new Vector3(
                (start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2)));
            for (int h = 0; h < height; h++)
            {
                Entity crate = spawnCrate((start + new Vector3(TXA, TYA, 10) + (new Vector3(0, 0, A.Z) * h)), angle);
                crate.Call("enablelinkto");
                crate.Call("linkto", center);
                for (int i = 0; i < blocks; i++)
                {
                    crate = spawnCrate(start + (new Vector3(A.X, A.Y, 0) * i) + new Vector3(0, 0, 10) + (new Vector3(0, 0, A.Z) * h), angle);
                    crate.Call("enablelinkto");
                    crate.Call("linkto", center);
                }
                crate = spawnCrate(new Vector3(end.X, end.Y, start.Z) + new Vector3(TXA * -1, TYA * -1, 10) + (new Vector3(0, 0, A.Z) * h), angle);
                crate.Call("enablelinkto");
                crate.Call("linkto", center);
            }
            return center;
        }
        public Entity CreateFloor(Vector3 corner1, Vector3 corner2)
        {
            float width = corner1.X - corner2.X;
            if (width < 0) width = width * -1;
            float length = corner1.Y - corner2.Y;
            if (length < 0) length = length * -1;

            int bwide = (int)Math.Round(width / 50, 0);
            int blength = (int)Math.Round(length / 30, 0);
            Vector3 C = corner2 - corner1;
            Vector3 A = new Vector3(C.X / bwide, C.Y / blength, 0);
            Entity center = Call<Entity>("spawn", "script_origin", new Parameter(new Vector3(
                (corner1.X + corner2.X) / 2, (corner1.Y + corner2.Y) / 2, corner1.Z)));
            for (int i = 0; i < bwide; i++)
            {
                for (int j = 0; j < blength; j++)
                {
                    Entity crate = spawnCrate(corner1 + (new Vector3(A.X, 0, 0) * i) + (new Vector3(0, A.Y, 0) * j), new Vector3(0, 0, 0));
                    crate.Call("enablelinkto");
                    crate.Call("linkto", center);
                }
            }
            return center;
        }

        private int _flagCount = 0;

        public void CreateElevator(Vector3 enter, Vector3 exit)
        {
            Entity flag = Call<Entity>("spawn", "script_model", new Parameter(enter));
            flag.Call("setModel", getAlliesFlagModel(_mapname));
            Entity flag2 = Call<Entity>("spawn", "script_model", new Parameter(exit));
            flag2.Call("setModel", getAxisFlagModel(_mapname));

            int curObjID = 31 - _flagCount++;
            Call(431, curObjID, "active"); // objective_add
            Call(435, curObjID, new Parameter(flag.Origin)); // objective_position
            Call(434, curObjID, "compass_waypoint_bomb"); // objective_icon

            OnInterval(100, () =>
            {
                foreach (Entity player in getPlayers())
                {
                    if (player.Origin.DistanceTo(enter) <= 50)
                    {
                        player.Call("setorigin", new Parameter(exit));
                    }
                }
                return true;
            });
        }

        public void CreateHiddenTP(Vector3 enter, Vector3 exit)
        {
            Entity flag = Call<Entity>("spawn", "script_model", new Parameter(enter));
            flag.Call("setModel", "weapon_scavenger_grenadebag");
            Entity flag2 = Call<Entity>("spawn", "script_model", new Parameter(exit));
            flag2.Call("setModel", "weapon_oma_pack");
            OnInterval(100, () =>
            {
                foreach (Entity player in getPlayers())
                {
                    if (player.Origin.DistanceTo(enter) <= 50)
                    {
                        player.Call("setorigin", new Parameter(exit));
                    }
                }
                return true;
            });
        }

        // maybe someday
        /*private void realElevator(Vector3 bottom, Vector3 top)
        {
            Entity center = Call<Entity>("spawn", "script_origin", new Parameter(bottom));
            Entity floor = CreateFloor(new Vector3(bottom.X - 200, bottom.Y - 150, bottom.Z),
                new Vector3(bottom.X + 200, bottom.Y + 150, bottom.Z));
            floor.Call("enablelinkto");
            floor.Call("linkto", center);
            Entity cieling = CreateFloor(new Vector3(bottom.X - 200, bottom.Y - 150, bottom.Z),
                new Vector3(bottom.X + 200, bottom.Y + 150, bottom.Z + 300));
            cieling.Call("enablelinkto");
            cieling.Call("linkto", center);
        }*/

        public Entity spawnCrate(Vector3 origin, Vector3 angles)
        {
            Entity ent = Call<Entity>("spawn", "script_model", new Parameter(origin));
            ent.Call("setmodel", "com_plasticcase_friendly");
            ent.SetField("angles", new Parameter(angles));
            ent.Call(33353, _airdropCollision); // clonebrushmodeltoscriptmodel
            return ent;
        }
        public Entity[] getSpawns(string name)
        {
            return Call<Entity[]>("getentarray", name, "classname");
        }
        public void removeSpawn(Entity spawn)
        {
            spawn.Call("delete");
        }
        public void createSpawn(string type, Vector3 origin, Vector3 angle)
        {
            Entity spawn = Call<Entity>("spawn", type, new Parameter(origin));
            spawn.SetField("angles", new Parameter(angle));
        }

        private static void print(string format, params object[] p)
        {
            Log.Write(LogLevel.All, format, p);
        }

        private void loadMapEdit(string mapname)
        {
            try
            {
                StreamReader map = new StreamReader("scripts\\maps\\" + mapname + ".txt");
                while (!map.EndOfStream)
                {
                    string line = map.ReadLine();
                    if (line.StartsWith("//") || line.Equals(string.Empty))
                    {
                        continue;
                    }
                    string[] split = line.Split(':');
                    if (split.Length < 1)
                    {
                        continue;
                    }
                    string type = split[0];
                    switch (type)
                    {
                        case "crate":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            spawnCrate(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        case "ramp":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            CreateRamp(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        case "elevator":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            CreateElevator(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        case "HiddenTP":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            CreateHiddenTP(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        case "door":
                            split = split[1].Split(';');
                            if (split.Length < 7) continue;
                            CreateDoor(parseVec3(split[0]), parseVec3(split[1]), parseVec3(split[2]), int.Parse(split[3]), int.Parse(split[4]), int.Parse(split[5]), int.Parse(split[6]));
                            break;
                        case "wall":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            CreateWall(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        case "floor":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            CreateFloor(parseVec3(split[0]), parseVec3(split[1]));
                            break;
                        /*case "realelevator":
                            split = split[1].Split(';');
                            if (split.Length < 2) continue;
                            realElevator(parseVec3(split[0]), parseVec3(split[1]));
                            break;*/
                        default:
                            print("Unknown MapEdit Entry {0}... ignoring", type);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                print("error loading mapedit for map {0}: {1}", mapname, e.Message);
            }
        }

        private Vector3 parseVec3(string vec3)
        {
            vec3 = vec3.Replace(" ", string.Empty);
            if (!vec3.StartsWith("(") && !vec3.EndsWith(")")) throw new IOException("Malformed MapEdit File!");
            vec3 = vec3.Replace("(", string.Empty);
            vec3 = vec3.Replace(")", string.Empty);
            String[] split = vec3.Split(',');
            if (split.Length < 3) throw new IOException("Malformed MapEdit File!");
            return new Vector3(float.Parse(split[0]), float.Parse(split[1]), float.Parse(split[2]));
        }

        private string getAlliesFlagModel(string mapname)
        {
            switch (mapname)
            {
                case "mp_alpha":
                case "mp_dome":
                case "mp_exchange":
                case "mp_hardhat":
                case "mp_interchange":
                case "mp_lambeth":
                case "mp_radar":
                case "mp_cement":
                case "mp_hillside_ss":
                case "mp_morningwood":
                case "mp_overwatch":
                case "mp_park":
                case "mp_qadeem":
                case "mp_restrepo_ss":
                case "mp_terminal_cls":
                    return "prop_flag_delta";
                case "mp_bootleg":
                case "mp_bravo":
                case "mp_carbon":
                case "mp_mogadishu":
                case "mp_village":
                    return "prop_flag_pmc";
                case "mp_paris":
                    return "prop_flag_gign";
                case "mp_plaza2":
                case "mp_seatown":
                case "mp_underground":
                case "mp_aground_ss":
                case "mp_courtyard_ss":
                case "mp_italy":
                case "mp_meteora":
                    return "prop_flag_sas";
            }
            return "";
        }
        private string getAxisFlagModel(string mapname)
        {
            switch (mapname)
            {
                case "mp_alpha":
                case "mp_bootleg":
                case "mp_dome":
                case "mp_exchange":
                case "mp_hardhat":
                case "mp_interchange":
                case "mp_lambeth":
                case "mp_paris":
                case "mp_plaza2":
                case "mp_radar":
                case "mp_underground":
                case "mp_cement":
                case "mp_hillside_ss":
                case "mp_overwatch":
                case "mp_park":
                case "mp_restrepo_ss":
                case "mp_terminal_cls":
                    return "prop_flag_speznas";
                case "mp_bravo":
                case "mp_carbon":
                case "mp_mogadishu":
                case "mp_village":
                    return "prop_flag_africa";
                case "mp_seatown":
                case "mp_aground_ss":
                case "mp_courtyard_ss":
                case "mp_meteora":
                case "mp_morningwood":
                case "mp_qadeem":
                case "mp_italy":
                    return "prop_flag_ic";
            }
            return "";
        }

        private Entity[] getPlayers()
        {
            List<Entity> players = new List<Entity>();
            for (int i = 0; i < 17; i++)
            {
                Entity entity = Call<Entity>("getentbynum", i);
                if (entity != null)
                {
                    if (entity.IsPlayer)
                    {
                        players.Add(entity);
                    }
                }
            }
            return players.ToArray();
        }
    }
}

#endregion