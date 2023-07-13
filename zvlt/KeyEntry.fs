module KeyEntry

open System
open System.Security

open FldVault.Core.Crypto
open FldVault.Core.Vaults

open ColorPrint
open CommonTools

let enterRawKey prompt =
  if prompt |> String.IsNullOrEmpty |> not then
    cp $"\fy%s{prompt}\f0: "
  let mutable complete = false
  let ss = new SecureString()
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
      ss.Clear()
      complete <- true
      cpx " \foAborted\f0"
    | _ ->
      ss.AppendChar(ki.KeyChar)
      Console.Write("*")
  cp ""
  if ss.Length >= 4 then
    ss
  else
    ss.Dispose()
    null

let enterKey prompt (salt: ReadOnlySpan<byte>) =
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
  PassphraseKey.FromSecureString(ss, salt)

let enterKeyFor (pkif:PassphraseKeyInfoFile) =
  cp $"\fyEnter passphrase for key \fo{pkif.KeyId}\f0:"
  let pk = enterKey null pkif.Salt
  if pk.GetId() <> pkif.KeyId then
    failwith "Incorrect passphrase"
  pk

let enterNewKey prompt =
  let saltBytes = PassphraseKey.GenerateSalt()
  let salt = new ReadOnlySpan<byte>(saltBytes)
  enterKey prompt salt
