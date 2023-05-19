module KeyEntry

open System
open System.Security

open FldVault.Core.Crypto

open ColorPrint
open CommonTools

let enterKey prompt salt =
  if prompt |> String.IsNullOrEmpty |> not then
    cp $"\fy%s{prompt}\f0: "
  let mutable complete = false
  use ss = new SecureString()
  while not complete do
    let ki = Console.ReadKey(true)
    match ki.Key with
    | ConsoleKey.Backspace ->
      if ss.Length>0 then
        ss.RemoveAt(ss.Length-1)
        Console.Write("\b \b")
      else
        Console.Write('\u0007')
    | ConsoleKey.Enter ->
      complete <- true
    | ConsoleKey.Escape ->
      failwith "Key entry aborted"
    | _ ->
      ss.AppendChar(ki.KeyChar)
      Console.Write("*")
  cp ""
  if ss.Length < 4 then
    failwith "Expecting at least 4 characters"
  PassphraseKey.FromSecureString(ss, salt, PassphraseKey.Saltlength)

let enterNewKey prompt =
  let saltBytes = PassphraseKey.GenerateSalt()
  let salt = new ReadOnlySpan<byte>(saltBytes)
  enterKey prompt salt
