@echo on
set AZURE_STORAGE_API_VERSION=2024-11-04
az storage blob upload ^
  --connection-string "UseDevelopmentStorage=true" ^
  --container-name call-center ^
  --file ".\audio\Support-Call.mp3" ^
  --name "Support-Call.mp3" ^
  --overwrite
