* [Durable] Add Version property to $Context
* Fix worker startup failure on the PowerShell 7.6 (.NET 10) worker for apps without a `profile.ps1`, by resolving the profile path with a direct existence check instead of a filtered directory enumeration (#1146)
