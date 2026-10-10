using DualOntologyWorldEngine.Checks;
return args.Length==2&&args[0]=="read" ? SessionSaveCheck.Read(args[1]) : SessionSaveCheck.Write(args.Length>1?args[1]:"tmp/core-save-check");
