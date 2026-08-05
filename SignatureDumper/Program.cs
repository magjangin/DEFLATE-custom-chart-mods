using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SignatureDumper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("  DEFLATE Assembly-CSharp Signature Dumper & Analyzer");
            Console.WriteLine("==========================================================");

            string gameDir = @"H:\steam\steamapps\common\DEFLATE";
            
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            
            if (!File.Exists(Path.Combine(solutionRoot, "DEFLATE custom chart.slnx")))
            {
                solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            }

            string outputDir = solutionRoot;

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])) gameDir = args[0];
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1])) outputDir = args[1];

            Console.WriteLine($"[Config] Game Directory:   {gameDir}");
            Console.WriteLine($"[Config] Output Directory: {outputDir}");
            Console.WriteLine("----------------------------------------------------------");

            try
            {
                var options = new SignatureDumperOptions
                {
                    GameDirectory = gameDir,
                    OutputDirectory = outputDir,
                    DecompileFolderName = "Decompiled",
                    DumpCSharpSignatures = false,
                    DumpSummaryText = false,
                    DumpJsonMetadata = false,
                    DumpIndividualFiles = true
                };

                var dumper = new AssemblySignatureDumper(options);
                dumper.Dump();

                Console.WriteLine("\n[Analysis] Checking Multiplayer vs Singleplayer indicators...");
                AnalyzeMultiplayer(Path.Combine(outputDir, "Decompiled"));

                Console.WriteLine("\n[Status] Signature Dumping & Analysis Completed Successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Error] Failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void AnalyzeMultiplayer(string decompiledDir)
        {
            if (!Directory.Exists(decompiledDir))
            {
                Console.WriteLine("Decompiled directory not found.");
                return;
            }

            var csFiles = Directory.GetFiles(decompiledDir, "*.cs", SearchOption.AllDirectories);
            Console.WriteLine($"Scanning {csFiles.Length} decompiled C# source files...");

            string[] mpKeywords = new[]
            {
                "Photon", "PUN", "PhotonNetwork", "Fusion", "Mirror", "NetworkBehaviour", 
                "NetworkServer", "NetworkClient", "NetworkIdentity", "ClientRpc", "ServerRpc",
                "SyncVar", "SteamNetworking", "Matchmaking", "Multiplayer", "NetworkManager",
                "P2P", "Lobby", "LobbyManager"
            };

            int matchCount = 0;

            foreach (var file in csFiles)
            {
                string content = File.ReadAllText(file);
                foreach (var kw in mpKeywords)
                {
                    if (Regex.IsMatch(content, $@"\b{kw}\b", RegexOptions.IgnoreCase))
                    {
                        Console.WriteLine($"  [Match] {Path.GetFileName(file)} -> '{kw}'");
                        matchCount++;
                    }
                }
            }

            Console.WriteLine("----------------------------------------------------------");
            if (matchCount == 0)
            {
                Console.WriteLine("[RESULT] 100% SINGLE-PLAYER (싱글 플레이 전용 게임)");
                Console.WriteLine("  - 멀티플레이어 / 네트워킹 관련 프레임워크(Photon, Mirror, Netcode, Steam P2P, RPC 등) 코드가 전혀 없습니다.");
                Console.WriteLine("  - 게임 오프라인 보존(Goldberg Emulator) 및 커스텀 로컬 모딩 진행 시 네트워크 분기 처리가 필요 없습니다.");
            }
            else
            {
                Console.WriteLine($"[RESULT] MULTIPLAYER / NETWORK CODE DETECTED ({matchCount} matches)");
            }
            Console.WriteLine("----------------------------------------------------------");
        }
    }
}
