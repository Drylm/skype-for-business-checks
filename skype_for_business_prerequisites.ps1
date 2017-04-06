function Read-Registry2($registryHive, $key, $propertyName) {
    $value= ""

    $localKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($registryHive, [Microsoft.Win32.RegistryView]::Registry64)
    $localKey = $localKey.OpenSubKey($key)
    if ($localKey -ne $null) {

        $property = $localKey.GetValue($propertyName)

        if ($property -eq $null) {
            throw New-Object System.Exception ("$propertyName has not been found")
        }

        $value = $property.ToString()
        return $value
    }

    $localKey32 = [Microsoft.Win32.RegistryKey]::OpenBaseKey($registryHive, [Microsoft.Win32.RegistryView]::Registry32)
    $localKey32 = $localKey32.OpenSubKey($key)
    if ($localKey32 -ne $null) {

        $property = $localKey32.GetValue($propertyName)

        if ($property -eq $null) {
            throw New-Object System.Exception ("$propertyName has not been found")
        }

        $value = $property.ToString()
        return $value
    }
    else {
        throw New-Object System.Exception ("Registry Key $key has not been found")
    }

    return $value
}

function Read-Registry($registryHive, $key, $propertyName) {
    try {
        $value = Read-Registry2 $registryHive $key $propertyName
        return $value
    }
    catch [System.Exception] {
        $message = $_.Exception.Message
        Write-Host $message -f "red"

        return ""
    }
}

function Check-SkypeForBusinessPrerequisites() {
    Try {
        Write-Host "Checks Prerequisites for a lync 2013 installation"

        Check-OfficeAdalAuthentication "15.0"
		    Check-SkypeForBusiness2013SDKPrerequisites
		    Check-SkypeForBusiness2013SDKUISuppressionMode

        Write-Host ""
        Write-Host "Checks Prerequisites for a skype for business 2016 installation"
		    Check-SkypeForBusiness2016SDKPrerequisites
	  } Catch [System.InvalidOperationException] {
        Write-Host $_.Exception.Message -f "red"
	  }

}

function Check-SkypeForBusiness2013SDKPrerequisites() {
    $lync2013Key = "SOFTWARE\Microsoft\Office\15.0\Registration\{0EA305CE-B708-4D79-8087-D636AB0F1A4D}"
    $lync2013Value = Read-Registry ([Microsoft.Win32.RegistryHive]::LocalMachine) $lync2013Key "ProductName"

    $office2013VersionKey = "SOFTWARE\Microsoft\Office\15.0\Common\ProductVersion"
	  $office2013VersionValue = Read-Registry ([Microsoft.Win32.RegistryHive]::LocalMachine) $office2013VersionKey "LastProduct"

    if ($lync2013Value -eq $null) {
        Write-Host "  - Lync 2013 not installed." -f "red"
    } else {
        Write-Host "  - Lync 2013 installation found: $lync2013Value."
    }

    if ($office2013VersionValue -eq $null) {
        Write-Host "  - Office 2013 not installed." -f "red"
    } else {
        Write-Host "  - Office 2013 installation found: $office2013VersionValue."
    }


    if (($lync2013Value -eq $null) -Or ($office2013VersionValue -eq $null)) {
		return
	}

	if (($lync2013Value -ne "Microsoft Lync 2013") -Or ($office2013VersionValue -ne "15.0.4569.1506")) {
		Write-Host "  - Lync 2013 retrieved: $lync2013Value - Expected : Microsoft Lync 2013 \nOffice 2013 version retrieved: $office2013Value - Expected : 15.0.4569.1506" -f Red
	}
}

function Check-OfficeAdalAuthentication($version) {
    $officeIdentityKey = "Software\Microsoft\Office\$version\Common\Identity"
    $adalValue = Read-Registry ([Microsoft.Win32.RegistryHive]::CurrentUser) $officeIdentityKey "EnableADAL"

	  if (($adalValue -eq $null) -Or ($adalValue -ne "0")) {
	      Write-Host "  - Please disable ADAL authentication mechanism for office $version" -f "red"
	  } else {
        Write-Host "  - ADAL Authentication for Office $version is disabled."
    }
}

function Check-SkypeForBusiness2013SDKUISuppressionMode() {
    $office2013LyncUISuppressionModeKey = "Software\Microsoft\Office\15.0\Lync"
    $office2013LyncUISuppressionModeValue = Read-Registry ([Microsoft.Win32.RegistryHive]::CurrentUser) $office2013LyncUISuppressionModeKey "UISuppressionMode"

	if (($office2013LyncUISuppressionModeValue -eq $null) -Or ($office2013LyncUISuppressionModeValue -ne "1")) {
	    Write-Host "  - Skype for Business client is not setup to run in UISuppressionMode. Please set the registry : $office2013LyncUISuppressionModeKey = 1" -f "red"
	} else {
      Write-Host "  - Skype for Business client is setup to run in UISuppressionMode."
    }
}

function Check-SkypeForBusiness2016SDKPrerequisites() {
    $lync2016Key = "SOFTWARE\Microsoft\Office\16.0\Registration\{03CA3B9A-0869-4749-8988-3CBC9D9F51BB}"
    $lync2016Value = Read-Registry ([Microsoft.Win32.RegistryHive]::LocalMachine) $lync2016Key "ProductName"
    if ($lync2016Value -ne "Skype for Business 2016") {
        Write-Host "  - Skype for Business 2016 not installed."
    } else {
        Write-Host "  - Skype for Business 2016 installation found: $lync2016Value."
    }

    Check-OfficeAdalAuthentication "16.0"
    Check-SkypeForBusiness2013SDKUISuppressionMode

    $office2016LyncUISuppressionModeKey = "Software\Microsoft\Office\16.0\Lync"
    $office2016LyncUISuppressionModeValue = Read-Registry ([Microsoft.Win32.RegistryHive]::CurrentUser) $office2016LyncUISuppressionModeKey "UISuppressionMode"

	if ($office2016LyncUISuppressionModeValue -ne "1") {
		  Write-Host "  - Skype for Business client 2016 has been detected and is not setup to run in UISuppressionMode. Please set the registry : $office2016LyncUISuppressionModeKey = 1" -f "red"
	} else {
      Write-Host "  - Skype for Business client 2016 is setup to run in UISuppressionMode."
  }

    Check-SkypeForBusiness2016ISignInConfigurationPatch
}

function Check-SkypeForBusiness2016ISignInConfigurationPatch() {
    Write-Host "  SignInConfiguration patch deployment check."
    $interfaceKey = "Interface\{61CE9972-C619-4A88-A5D1-D2DFBCD4D2A1}"
    $interfaceValue = Read-Registry ([Microsoft.Win32.RegistryHive]::ClassesRoot) $interfaceKey ""

    $proxyStubClsid32Key = "$interfaceKey\ProxyStubClsid32"
    $proxyStubClsid32Value = Read-Registry ([Microsoft.Win32.RegistryHive]::ClassesRoot) $proxyStubClsid32Key ""

    $typeLibKey = "$interfaceKey\TypeLib"
    $typeLibValue = Read-Registry ([Microsoft.Win32.RegistryHive]::ClassesRoot) $typeLibKey ""
    $typeLibVersion = Read-Registry ([Microsoft.Win32.RegistryHive]::ClassesRoot) $typeLibKey "Version"

    if (($interfaceValue -ne "ISignInConfiguration2") -Or ($proxyStubClsid32Value -ne "{00020424-0000-0000-C000-000000000046}") -Or
        ($typeLibValue -ne "{B9AA1F11-F480-4054-A84E-B5D9277E40A8}") -Or ($typeLibVersion -ne "1.0")) {

            Write-Host "  - SignInConfiguration patch not deployed." -f "red"
            Write-Host "    - Run the script sb4-skype4Business 2016.reg"
            Write-Host "    - Interface value = $interfaceValue - expected ISignInConfiguration2"
            Write-Host "    - ProxyStubClsid32 value = $proxyStubClsid32Value - expected {00020424-0000-0000-C000-000000000046}"
            Write-Host "    - TypeLib value = $typeLibValue - expected {B9AA1F11-F480-4054-A84E-B5D9277E40A8}"
            Write-Host "    - TypeLib version value = $typeLibVersion - expected 1.0"
    }
    else {
        Write-Host "  - SignInConfiguration patch correctly deployed."
    }
}

Check-SkypeForBusinessPrerequisites
