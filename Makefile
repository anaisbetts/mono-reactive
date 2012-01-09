
RUNTIME = mono

all: System.Reactive.Tests/bin/Debug/System.Reactive.Tests.dll

System.Reactive/bin/Debug/System.Reactive.dll:
	xbuild

System.Reactive.Tests/bin/Debug/System.Reactive.Tests.dll: System.Reactive/bin/Debug/System.Reactive.dll
	xbuild

run-test: all
	$(RUNTIME) external/nunit26/nunit-console.exe System.Reactive.Tests/bin/Debug/System.Reactive.Tests.dll
