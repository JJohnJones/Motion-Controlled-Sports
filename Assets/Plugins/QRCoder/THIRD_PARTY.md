# Managed QR encoder dependencies

QRCoder 1.8.0 (MIT), upstream https://github.com/Shane32/QRCoder at commit `443d5a1f76debf203b1e252efee6996a15d41f5c`.

`QRCoder.dll` is the unmodified `lib/netstandard1.3/QRCoder.dll` from https://api.nuget.org/v3-flatcontainer/qrcoder/1.8.0/qrcoder.1.8.0.nupkg . SHA-256:
`167a7dcbdd73cfcb874f89efb571807e6b6e287b15a50561855edf2945953edf`.

System.Text.Encoding.CodePages 5.0.0 (MIT), Microsoft. `System.Text.Encoding.CodePages.dll` is the unmodified `lib/netstandard1.3` assembly from https://api.nuget.org/v3-flatcontainer/system.text.encoding.codepages/5.0.0/system.text.encoding.codepages.5.0.0.nupkg . SHA-256:
`8981259e2062f4d2ca218e9d21fec949ce4b29a84a85d6c92df9086c0917f534`.

Both license texts are included here. The .NET Standard 1.3 assemblies avoid QRCoder's optional System.Drawing/WPF renderer dependencies. Unity renders the encoder's matrix directly, including its quiet border, using point filtering. No NuGet manager, native QR plugin, runtime download or remote QR service is required. Preserve these files and their `.meta` files in the Unity repository. Revalidate a Windows IL2CPP player after changing or upgrading these managed assemblies.
