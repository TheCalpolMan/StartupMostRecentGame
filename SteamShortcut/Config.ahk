global dataFile := FileRead("data.txt")

apiKey := ""
if("" != FileExist("key.txt"))
{
    apiKey := FileRead("key.txt")
}

steamIDStartPos := findWithinString("steamid`":", dataFile, true)
; MsgBox(SubStr(dataFile, steamIDStartPos))
steamID := readStringFromString(dataFile, steamIDStartPos)

TPC := "2" ; Text Position Centering, used to center text relative to edit boxes

ConfigGUI := Gui(, "Config Editor")
ConfigGUI.AddText("X5 Y" . TPC , "Steam web API key:")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w240 Uppercase vAPIInput", apiKey)

ConfigGUI.AddText("X5 Y+" . TPC , "Steam ID:")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w120 Number vIDInput", steamID)

ConfigGUI.AddButton("XP+90 Y+ vSaveButton", "Save")

ConfigGUI["SaveButton"].OnEvent("Click", SaveFields)
SaveFields(caller, mouseButton) ; caller refers to SaveButton
{
    keyFile := FileOpen("key.txt", "w")
    keyFile.Write(ConfigGUI["APIInput"].Text)

    global dataFile := SubStr(dataFile, 1, steamIDStartPos) . ConfigGUI["IDInput"].Text . SubStr(dataFile, steamIDStartPos + StrLen(steamID) + 1)
    dataFileWrite := FileOpen("data.txt", "w")
    dataFileWrite.Write(dataFile)

    MsgBox("Fields saved!","",)
    ConfigGUI.Destroy()
}

ConfigGUI.Show()



findWithinString(target, source, findAfter)
{
    i := 0

    loop StrLen(source)
    {
        i := A_Index

        if(SubStr(source, i, 1) == SubStr(target, 1, 1))
        {
            
            if(SubStr(source, i, StrLen(target)) == target)
            {
                if(findAfter)
                    return i + StrLen(target)
                else
                    return i
            }
        }
    }

    return -1
}

readStringFromString(toReadFrom, startPoint)
{
    if(SubStr(toReadFrom, startPoint, 1) != "`"")
        throw ValueError("startPoint should point to an occurance of '`"'")


    loop StrLen(toReadFrom) - startPoint + 1
    {
        i := A_Index + startPoint

        if(SubStr(toReadFrom, i, 1) == "`"")
        {
            return SubStr(toReadFrom, startPoint + 1, i - startPoint - 1)
        }
    }

    return ""
}