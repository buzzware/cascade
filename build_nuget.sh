cd StandardExceptionsDotNet/Buzzware.StandardExceptions
dotnet build -c Release && dotnet pack -c Release
cd -
cd Buzzware.Cascade
dotnet build -c Release && dotnet pack -c Release
cd -
# dotnet nuget push "bin/Release/Buzzware.Cascade.x.y.z.nupkg" --source "https://api.nuget.org/v3/index.json" --api-key "API KEY"
