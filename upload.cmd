@echo on
az storage blob upload ^
  --account-name rpresearchstorage ^
  --auth-mode login ^
  --container-name call-center ^
  --file ".\audio\Support-Call.mp3" ^
  --name "Support-Call.mp3" ^
  --overwrite
