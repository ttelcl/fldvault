module CommandFile

open System
open System.IO

open ColorPrint

let expandCommandFile fileName =
  if fileName |> File.Exists |> not then
    cp $"\foCommand file '\fy{fileName}\fo' not found\f0."
    None
  else
    let lines = File.ReadAllLines fileName
    let args = []
    let processLine args (line: string)  =
      let line = line.Trim()
      if line.StartsWith("#") || String.IsNullOrWhiteSpace(line) then
        args
      elif line.StartsWith("-") then
        let parts = line.Split([| ' ' |], 2)
        match parts.Length with
        | 0 -> args
        | 1 -> parts[0] :: args
        | 2 -> parts[1] :: parts[0] :: args
        | _ -> failwith "Unexpected line format"
      else
        line :: args
    lines |> Array.fold processLine args |> List.rev |> Some
