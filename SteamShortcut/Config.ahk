global dataFile := FileRead("data.txt")

apiKey := ""
if("" != FileExist("key.txt"))
{
    apiKey := FileRead("key.txt")
}

steamIDStartPos := findWithinString("steamid`":", dataFile, true)
linkAmountStartPos := findWithinString("linkAmount`":", dataFile, true) - 1
sleepBetweenRenamesStartPos := findWithinString("sleepBetweenRenames`":", dataFile, true) - 1
sleepBeforeIconChangeStartPos := findWithinString("sleepBeforeIconChange`":", dataFile, true) - 1
; MsgBox(SubStr(dataFile, steamIDStartPos))
steamID := readStringFromString(dataFile, steamIDStartPos)
linkAmount := readIntFromString(dataFile, linkAmountStartPos)
sleepBetweenRenames := readIntFromString(dataFile, sleepBetweenRenamesStartPos)
sleepBeforeIconChange := readIntFromString(dataFile, sleepBeforeIconChangeStartPos)

TPC := "2" ; Text Position Centering, used to center text relative to edit boxes

ConfigGUI := Gui(, "Config Editor")
ConfigGUI.AddText("X5 Y" . TPC , "Steam web API key:")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w240 Uppercase vAPIInput", apiKey)

ConfigGUI.AddText("X5 Y+" . TPC , "Steam ID:")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w120 Number vIDInput", steamID)

ConfigGUI.AddText("X5 Y+" . TPC , "Number of desktop shortcuts:")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w120 Number vLinkNo", linkAmount)

ConfigGUI.AddText("X5 Y+" . TPC , "Sleep time between file renames (ms):")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w120 Number vFileSleep", sleepBetweenRenames)

ConfigGUI.AddText("X5 Y+" . TPC , "Sleep time before file icons changed (ms):")
ConfigGUI.AddEdit("X+5 YP-" . TPC . " r1 w120 Number vIconSleep", sleepBeforeIconChange)

ConfigGUI.AddButton("X160 Y+ vSaveButton", "Save")

ConfigGUI["SaveButton"].OnEvent("Click", SaveFields)
SaveFields(caller, mouseButton) ; caller refers to SaveButton
{
    keyFile := FileOpen("key.txt", "w")
    keyFile.Write(ConfigGUI["APIInput"].Text)

    global dataFile := SubStr(dataFile, 1, steamIDStartPos) . ConfigGUI["IDInput"].Text . SubStr(dataFile, steamIDStartPos + StrLen(steamID) + 1)
    global linkAmountStartPos := findWithinString("linkAmount`":", dataFile, true) - 1
    global dataFile := SubStr(dataFile, 1, linkAmountStartPos) . ConfigGUI["LinkNo"].Text . SubStr(dataFile, linkAmountStartPos + StrLen(linkAmount) + 1)
    global sleepBetweenRenamesStartPos := findWithinString("sleepBetweenRenames`":", dataFile, true) - 1
    global dataFile := SubStr(dataFile, 1, sleepBetweenRenamesStartPos) . ConfigGUI["FileSleep"].Text . SubStr(dataFile, sleepBetweenRenamesStartPos + StrLen(sleepBetweenRenames) + 1)
    global sleepBeforeIconChangeStartPos := findWithinString("sleepBeforeIconChange`":", dataFile, true) - 1
    global dataFile := SubStr(dataFile, 1, sleepBeforeIconChangeStartPos) . ConfigGUI["IconSleep"].Text . SubStr(dataFile, sleepBeforeIconChangeStartPos + StrLen(sleepBeforeIconChange) + 1)

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

readIntFromString(toReadFrom, startPoint)
{
    loop StrLen(toReadFrom) - startPoint + 1
    {
        i := A_Index + startPoint

        if(SubStr(toReadFrom, i, 1) == ",")
        {
            return Integer(SubStr(toReadFrom, startPoint + 1, i - startPoint - 1))
        }
    }
    
    return 0
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