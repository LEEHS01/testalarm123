using HNS_Alarm.Module;


var mod = new AlarmModule(new AlarmModule.Parameter() {
    dbUrl = "http://192.168.1.20:2000/", 
    intervalSec = 10
});

mod.OnInternalMessage += Console.Write;

if (!mod.Start()) return;
Console.WriteLine("Press any key to stop the module...");
Console.ReadKey();
if (!mod.Quit()) return;
Console.WriteLine("Process Ended");



//if (!mod.Start()) return;
//Console.WriteLine("started");
//Console.ReadKey();
//if (!mod.Quit()) return;
//Console.WriteLine("quitted");
//Console.ReadKey();
//if (!mod.Start()) return;
//Console.WriteLine("started");
//Console.ReadKey();
//if (!mod.Quit()) return;
//Console.WriteLine("quitted");

