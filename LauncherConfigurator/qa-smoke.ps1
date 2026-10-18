$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$launcherOutput = Join-Path $projectRoot 'Launcher\bin\Release\net8.0-windows'
$tempTarget = Join-Path $PSScriptRoot '.qa-smoke-target'
New-Item -ItemType Directory -Force -Path $tempTarget | Out-Null
Copy-Item -LiteralPath (Join-Path $launcherOutput 'appsettings.json') -Destination $tempTarget

function Get-PrivateField($instance, [string] $name) {
    $value = $instance.GetType().GetField($name, [Reflection.BindingFlags] 'Instance, NonPublic').GetValue($instance)
    return ,$value
}

function Invoke-PrivateMethod($instance, [string] $name, [object[]] $arguments = @()) {
    $method = $instance.GetType().GetMethod($name, [Reflection.BindingFlags] 'Instance, NonPublic')
    $rawArguments = [object[]]::new($arguments.Count)
    for ($i = 0; $i -lt $arguments.Count; $i++) {
        $rawArguments[$i] = $arguments[$i].PSObject.BaseObject
    }
    $method.Invoke($instance, $rawArguments)
}

function Get-DescendantControls($control) {
    foreach ($child in $control.Controls) {
        $child
        Get-DescendantControls $child
    }
}

try {
    $launcherAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $launcherOutput 'Launcher.dll'))
    $mainForm = [Activator]::CreateInstance($launcherAssembly.GetType('Launcher.MainForm'))
    $root = Get-PrivateField $mainForm '_root'
    $navigation = Get-PrivateField $mainForm '_navigationPanel'
    $navigationButtons = Get-PrivateField $mainForm '_navigationButtons'
    $languageButton = Get-PrivateField $mainForm '_languageButton'
    $accountButton = Get-PrivateField $mainForm '_accountButton'
    $signInSubmit = Get-PrivateField $mainForm '_signInSubmitButton'
    $showPassword = Get-PrivateField $mainForm '_showPasswordButton'
    $legalCheckBox = Get-PrivateField $mainForm '_legalCheckBox'
    $createAccountLink = Get-PrivateField $mainForm '_createAccountLink'
    $mainForm.StartPosition = [Windows.Forms.FormStartPosition]::Manual
    $mainForm.Location = [Drawing.Point]::new(-2000, -2000)
    $mainForm.Opacity = 1
    $mainForm.ShowInTaskbar = $false
    $mainForm.CreateControl()
    $root.CreateControl()
    $mainForm.Show()
    [Windows.Forms.Application]::DoEvents()

    if ($accountButton.Text -ne 'SIGN IN' -or $signInSubmit.Text -ne 'SIGN IN') {
        throw "English sign-in labels are incorrect: header='$($accountButton.Text)', submit='$($signInSubmit.Text)'."
    }
    if ([Math]::Abs($accountButton.Font.Size - 20) -gt 0.1 -or -not $accountButton.Font.Bold) {
        throw 'Header sign-in action should use the same 20 pt bold styling as the navigation tabs.'
    }
    foreach ($control in @((Get-PrivateField $mainForm '_signInEmailLabel'), (Get-PrivateField $mainForm '_signInPasswordLabel'), (Get-PrivateField $mainForm '_emailTextBox'), (Get-PrivateField $mainForm '_passwordTextBox'))) {
        if ([Math]::Abs($control.Font.Size - 17) -gt 0.1) { throw "Sign-in field typography is incorrect: $($control.Font.Size) pt." }
    }
    foreach ($control in @($legalCheckBox, $createAccountLink)) {
        if ([Math]::Abs($control.Font.Size - 16) -gt 0.1) { throw "Sign-in agreement/link typography is incorrect: $($control.Font.Size) pt." }
    }
    foreach ($button in @($languageButton) + @($navigationButtons.Values)) {
        if ([Math]::Abs($button.Font.Size - 20) -gt 0.1 -or -not $button.Font.Bold) {
            throw "Header typography does not match: '$($button.Text)' uses $($button.Font.Size) pt, bold=$($button.Font.Bold)."
        }
    }

    foreach ($size in @(@(1020, 650), @(1280, 780), @(1920, 1080))) {
        $mainForm.ClientSize = [Drawing.Size]::new($size[0], $size[1])
        $mainForm.PerformLayout()
        $root.PerformLayout()
        $navigation.PerformLayout()
        $headerLayout = $root.GetControlFromPosition(0, 0).Controls[0]
        $accountHost = $accountButton.Parent
        $headerLayout.PerformLayout()
        $accountHost.PerformLayout()
        if (($accountHost.Left -lt 0) -or ($accountHost.Right -gt $headerLayout.ClientSize.Width) -or ($accountHost.Top -lt 0) -or ($accountHost.Bottom -gt $headerLayout.ClientSize.Height) -or ($accountButton.Left -lt 0) -or ($accountButton.Right -gt $accountHost.ClientSize.Width) -or ($accountButton.Top -lt 0) -or ($accountButton.Bottom -gt $accountHost.ClientSize.Height)) {
            throw "Header sign-in button is clipped at client size $($size[0])x$($size[1]): button=$($accountButton.Bounds), host=$($accountHost.ClientSize), header=$($headerLayout.ClientSize)."
        }
        $ordered = @($navigationButtons.Values | Sort-Object Left)
        for ($i = 1; $i -lt $ordered.Count; $i++) {
            if ($ordered[$i - 1].Right -gt $ordered[$i].Left) {
                throw "Header buttons overlap at client size $($size[0])x$($size[1])."
            }
        }
        if ($ordered.Count -gt 0 -and $ordered[-1].Right -gt $navigation.ClientSize.Width) {
            throw "Header buttons overflow at client size $($size[0])x$($size[1])."
        }

        $signInPage = Get-PrivateField $mainForm '_signInPage'
        Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('SignIn') | Out-Null
        [Windows.Forms.Application]::DoEvents()
        $signInPage.PerformLayout()
        $signInLayout = $signInPage.Controls[0]
        $signInLayout.PerformLayout()
        $artPanel = Get-PrivateField $mainForm '_signInArtPanel'
        $art = Get-PrivateField $mainForm '_signInBackground'
        $expectedArtWidth = [Math]::Round($artPanel.ClientSize.Width * (2.0 / 3.0))
        $expectedArtHeight = [Math]::Round($artPanel.ClientSize.Height * (2.0 / 3.0))
        if ([Math]::Abs($art.Width - $expectedArtWidth) -gt 1 -or [Math]::Abs($art.Height - $expectedArtHeight) -gt 1) {
            throw "Sign-in art is not sized to two-thirds of its panel at $($size[0])x$($size[1]): image=$($art.Size), panel=$($artPanel.ClientSize)."
        }
        $formPanel = $signInLayout.GetControlFromPosition(1, 0)
        $formPanel.PerformLayout()
        $formTable = $formPanel.Controls[0]
        $formTable.PerformLayout()
        $eyebrow = Get-PrivateField $mainForm '_signInEyebrow'
        $heading = Get-PrivateField $mainForm '_signInHeading'
        $description = Get-PrivateField $mainForm '_signInDescription'
        if ($heading.Top -lt $eyebrow.Bottom -or $description.Top -lt $heading.Bottom) {
            throw "Sign-in heading overlaps neighboring text at client size $($size[0])x$($size[1])."
        }
        if ($heading.Height -lt $heading.PreferredHeight) {
            throw "Sign-in heading '$($heading.Text)' is vertically clipped at client size $($size[0])x$($size[1])."
        }
        foreach ($name in @('_signInHeading', '_emailTextBox', '_passwordTextBox', '_showPasswordButton', '_signInSubmitButton', '_createAccountLink')) {
            $control = Get-PrivateField $mainForm $name
            if ($control.Bottom -gt $formTable.ClientSize.Height) {
                throw "Sign-in control $name is clipped at client size $($size[0])x$($size[1])."
            }
        }
        $toggleParent = $showPassword.Parent
        if (-not $showPassword.Visible -or $showPassword.Right -gt $toggleParent.ClientSize.Width -or $showPassword.Bottom -gt $toggleParent.ClientSize.Height) {
            throw "Show Password is clipped or hidden at client size $($size[0])x$($size[1]): button=$($showPassword.Bounds), host=$($toggleParent.ClientSize)."
        }

        Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('Play') | Out-Null
        $playPage = Get-PrivateField $mainForm '_playPage'
        if ($playPage.Controls.Count -ne 1) { throw 'The Play page still contains an extra logo/banner panel.' }
        $controlPanel = $playPage.Controls[0]
        $controlsTable = $controlPanel.Controls[0]
        $maintenance = $controlsTable.GetControlFromPosition(1, 0)
        $maintenanceButtons = $maintenance.GetControlFromPosition(0, 0)
        $maintenance.PerformLayout()
        $maintenanceButtons.PerformLayout()
        $checkButton = Get-PrivateField $mainForm '_checkButton'
        $repairButton = Get-PrivateField $mainForm '_repairButton'
        if ($checkButton.Left -lt 0 -or $repairButton.Right -gt $maintenanceButtons.ClientSize.Width) {
            throw "Check/Repair buttons are clipped at client size $($size[0])x$($size[1])."
        }
        $versionLabel = Get-PrivateField $mainForm '_versionLabel'
        if ($versionLabel.Parent.GetRow($versionLabel) -ne 1 -or $versionLabel.Bottom -gt $maintenance.ClientSize.Height) {
            throw 'The server version label is not fully visible below Check/Repair.'
        }
        $playButtonHost = $controlsTable.GetControlFromPosition(2, 0)
        $playButtonHost.PerformLayout()
        $playButton = Get-PrivateField $mainForm '_playButton'
        if ($playButton.Left -lt 0 -or $playButton.Right -gt $playButtonHost.ClientSize.Width) {
            throw "Play Game button is clipped at client size $($size[0])x$($size[1]): button=$($playButton.Bounds), host=$($playButtonHost.ClientSize)."
        }
        $playPictures = @(Get-DescendantControls $playPage | Where-Object { $_ -is [Windows.Forms.PictureBox] })
        if ($playPictures.Count -gt 0) { throw 'The Play page still contains a duplicate game logo image.' }
        Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('News') | Out-Null
    }

    $transitionTimer = Get-PrivateField $mainForm '_pageTransitionTimer'
    $transitionOverlay = Get-PrivateField $mainForm '_wipeTransition'
    $contentHost = Get-PrivateField $mainForm '_contentHost'
    $transitionWatch = [Diagnostics.Stopwatch]::StartNew()
    Invoke-PrivateMethod $mainForm 'ShowPage' @('SignIn') | Out-Null
    if (-not $transitionTimer.Enabled) { throw 'The page wipe did not start after a tab change.' }
    if (-not $transitionOverlay.Visible -or $contentHost.Enabled) {
        throw 'The page wipe did not cover the content or lock page controls during transition.'
    }
    while ($transitionTimer.Enabled -and $transitionWatch.ElapsedMilliseconds -lt 2500) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 10
    }
    $transitionWatch.Stop()
    if ($transitionTimer.Enabled) { throw 'The page wipe did not finish within 2.5 seconds.' }
    if ($transitionWatch.ElapsedMilliseconds -lt 850 -or $transitionWatch.ElapsedMilliseconds -gt 1600) {
        throw "The page wipe duration is outside the expected 1 second timing: $($transitionWatch.ElapsedMilliseconds) ms."
    }
    if ([Math]::Abs($mainForm.Opacity - 1) -gt 0.02 -or (Get-PrivateField $mainForm '_currentPage') -ne 'SignIn') {
        throw 'The page wipe changed window opacity or failed to show the requested page.'
    }
    if ($transitionOverlay.Visible -or -not $contentHost.Enabled) {
        throw 'The page wipe overlay or disabled content state remained after the transition.'
    }

    $password = Get-PrivateField $mainForm '_passwordTextBox'
    $password.Text = 'smoke-test-secret'
    $showPassword.PerformClick()
    if ($password.UseSystemPasswordChar -or $showPassword.Text -ne 'HIDE') {
        throw 'The Show Password control did not reveal text or update its label.'
    }
    $showPassword.PerformClick()
    if (-not $password.UseSystemPasswordChar -or $showPassword.Text -ne 'SHOW') {
        throw 'The Show Password control did not restore password masking.'
    }

    $playPage = Get-PrivateField $mainForm '_playPage'
    if ($playPage.Controls.Count -ne 1) { throw 'The Play page still contains an extra logo/banner panel.' }
    $playPictures = @(Get-DescendantControls $playPage | Where-Object { $_ -is [Windows.Forms.PictureBox] })
    if ($playPictures.Count -gt 0) { throw 'The Play page still contains a duplicate game logo image.' }
    $versionLabel = Get-PrivateField $mainForm '_versionLabel'
    if ($versionLabel.Parent.GetRow($versionLabel) -ne 1) { throw 'The server version label is not below the Check Update/Repair row.' }
    if ((Get-PrivateField $mainForm '_progressBar').GetType().Name -ne 'CyberProgressBar') {
        throw 'The Play page is not using the segmented cyber progress control.'
    }
    Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('News') | Out-Null

    $mainForm.ClientSize = [Drawing.Size]::new(1280, 780)
    $mainForm.PerformLayout()
    [Windows.Forms.Application]::DoEvents()
    Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('SignIn') | Out-Null
    $art = Get-PrivateField $mainForm '_signInBackground'
    if ($null -eq $art.Image) {
        $art.Image = [Drawing.Bitmap]::new(2, 2)
    }
    $baseTop = $mainForm.GetType().GetField('_signInImageBaseTop', [Reflection.BindingFlags] 'Instance, NonPublic').GetValue($mainForm)
    $animationTimer = Get-PrivateField $mainForm '_signInImageAnimationTimer'
    if (-not $animationTimer.Enabled) { throw 'Sign-in image animation did not start when the Sign-in page opened.' }
    Start-Sleep -Milliseconds 1050
    Invoke-PrivateMethod $mainForm 'AnimateSignInImage' | Out-Null
    $expectedTravel = [Math]::Round($mainForm.DeviceDpi * 0.5 / 2.54)
    if ([Math]::Abs($art.Top - ($baseTop - $expectedTravel)) -gt 1) {
        throw "Sign-in art did not reach approximately 0.5 cm upward after one second: top=$($art.Top), base=$baseTop, travel=$expectedTravel."
    }
    Start-Sleep -Milliseconds 1050
    Invoke-PrivateMethod $mainForm 'AnimateSignInImage' | Out-Null
    if ([Math]::Abs($art.Top - $baseTop) -gt 1) {
        throw "Sign-in art did not return to its base position after the down cycle: top=$($art.Top), base=$baseTop."
    }
    Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('News') | Out-Null
    if ($animationTimer.Enabled -or $art.Top -ne $baseTop) {
        throw 'Sign-in art animation did not stop and reset after navigating away.'
    }

    $authType = $launcherAssembly.GetType('Launcher.Services.AuthSession')
    $session = [Activator]::CreateInstance($authType, [object[]] @('smoke-token', 'qa@example.test', 'QA User'))
    $manifest = [Activator]::CreateInstance($launcherAssembly.GetType('Launcher.Models.GameManifest'))
    Get-PrivateField $mainForm '_authSession' | Out-Null
    $flags = [Reflection.BindingFlags] 'Instance, NonPublic'
    $mainForm.GetType().GetField('_authSession', $flags).SetValue($mainForm, $session)
    $mainForm.GetType().GetField('_manifest', $flags).SetValue($mainForm, $manifest)
    $mainForm.GetType().GetField('_busy', $flags).SetValue($mainForm, $true)
    Invoke-PrivateMethod $mainForm 'ApplyPageImmediately' @('Play') | Out-Null
    $mainForm.GetType().GetField('_busy', $flags).SetValue($mainForm, $false)
    Invoke-PrivateMethod $mainForm 'UpdatePlayAvailability' | Out-Null
    foreach ($name in @('_checkButton', '_repairButton', '_playButton')) {
        if (-not (Get-PrivateField $mainForm $name).Enabled) {
            throw "Expected $name to be enabled after the signed-in state is restored."
        }
    }
    Write-Output 'Launcher smoke check passed: header/logo, 1 second Wipe, Play layout/progress, Sign-in typography/toggle/bounds, and signed-in action buttons.'
    $mainForm.Close()
    $mainForm.Dispose()

    $configAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\LauncherConfigurator.dll'))
    $configForm = [Activator]::CreateInstance($configAssembly.GetType('LauncherConfigurator.ConfiguratorForm'))
    $configForm.Opacity = 0
    $configForm.ShowInTaskbar = $false
    $configForm.CreateControl()
    $configForm.Show()
    [Windows.Forms.Application]::DoEvents()
    $configRoot = $configForm.Controls[0]
    $bodyHost = $configRoot.GetControlFromPosition(0, 1)
    $body = $bodyHost.Controls[0]
    $brandPage = $body.TabPages[0]
    $body.SelectedTab = $brandPage
    foreach ($size in @(@(1020, 700), @(1080, 790), @(1920, 1080))) {
        $configForm.ClientSize = [Drawing.Size]::new($size[0], $size[1])
        $configForm.PerformLayout()
        $configRoot.PerformLayout()
        $bodyHost.PerformLayout()
        $body.PerformLayout()
        if ($body.Width -gt 1360) { throw "Configurator tab content exceeded its maximum width at $($size[0])x$($size[1])." }

        [Windows.Forms.Application]::DoEvents()
        $brandPage.PerformLayout()
        $brandGrid = $brandPage.Controls[0]
        $brandGrid.PerformLayout()
        foreach ($control in $brandGrid.Controls) {
            if ($control.Left -lt 0 -or $control.Right -gt $brandPage.ClientSize.Width) {
                throw "Brand editor control '$($control.Text)' is clipped at $($size[0])x$($size[1]): control=$($control.Bounds), grid=$($brandGrid.Bounds), page=$($brandPage.ClientSize)."
            }
        }
    }
    $newsPage = $body.TabPages[1]
    $body.SelectedTab = $newsPage
    [Windows.Forms.Application]::DoEvents()
    $newsLayout = $newsPage.Controls[0]
    $listPanel = $newsLayout.GetControlFromPosition(0, 0)
    $listButtons = $listPanel.GetControlFromPosition(0, 2)
    $listButtons.PerformLayout()
    $confirmButton = Get-PrivateField $configForm '_newsConfirmButton'
    if ($confirmButton.Right -gt $listButtons.ClientSize.Width -or $confirmButton.Bottom -gt $listButtons.ClientSize.Height) {
        throw 'News confirmation button is clipped inside the News toolbar.'
    }

    Invoke-PrivateMethod $configForm 'LoadTarget' @($tempTarget) | Out-Null
    $document = Get-PrivateField $configForm '_document'
    if ($null -eq $document) {
        throw "Configurator did not load the smoke-copy JSON. Folder: $((Get-PrivateField $configForm '_targetFolder').Text); status: $((Get-PrivateField $configForm '_status').Text)"
    }
    $news = $document['Launcher']['News']['Posts']
    $baselineCount = $news.Count
    if ($baselineCount -lt 1) { throw 'The smoke copy must contain at least one News post.' }
    $baselineTitles = @($news | ForEach-Object { $_['Title'].GetValue[string]() })

    $logoPath = Join-Path $launcherOutput 'Assets\launcher-logo.png'
    $relativeLogo = Invoke-PrivateMethod $configForm 'CopyAsset' @($logoPath, 'Branding', 'qa-logo')
    if (-not (Test-Path -LiteralPath (Join-Path $tempTarget $relativeLogo))) {
        throw 'Brand image import did not copy the selected image into the target launcher folder.'
    }
    $configFields = Get-PrivateField $configForm '_fields'
    $configFields['Launcher.Branding.Logo'].Text = $relativeLogo

    Invoke-PrivateMethod $configForm 'AddNewsPost' | Out-Null
    (Get-PrivateField $configForm '_newsTitleEn').Text = 'QA distinct article'
    (Get-PrivateField $configForm '_newsTitleVn').Text = 'Bài kiểm tra riêng'
    (Get-PrivateField $configForm '_newsSummaryEn').Text = 'Second post must stay independent.'
    Invoke-PrivateMethod $configForm 'ConfirmCurrentNews' | Out-Null

    $newsList = Get-PrivateField $configForm '_newsList'
    $newsList.SelectedIndex = 0
    (Get-PrivateField $configForm '_newsSummaryEn').Text = 'First post edited independently.'
    Invoke-PrivateMethod $configForm 'ConfirmCurrentNews' | Out-Null
    Invoke-PrivateMethod $configForm 'SaveConfig' | Out-Null

    $saved = Get-Content -LiteralPath (Join-Path $tempTarget 'appsettings.json') -Raw | ConvertFrom-Json
    $savedPosts = @($saved.Launcher.News.Posts)
    if ($savedPosts.Count -ne ($baselineCount + 1)) { throw 'News save changed the number of posts unexpectedly.' }
    if ($savedPosts[0].Summary -ne 'First post edited independently.') { throw 'First News post edit did not persist.' }
    if ($savedPosts[$baselineCount].Title -ne 'QA distinct article') { throw 'The new News article title did not persist.' }
    if ($savedPosts[$baselineCount].Summary -ne 'Second post must stay independent.') { throw 'The new News article content was overwritten.' }
    if ($saved.Launcher.Branding.Logo -ne $relativeLogo) { throw 'The imported logo path was not saved into appsettings.json.' }
    for ($i = 1; $i -lt $baselineCount; $i++) {
        if ($savedPosts[$i].Title -ne $baselineTitles[$i]) { throw "Existing News post $i was overwritten by another post." }
    }
    Invoke-PrivateMethod $configForm 'LoadTarget' @($tempTarget) | Out-Null
    if ((Get-PrivateField $configForm '_newsList').Items.Count -ne ($baselineCount + 1)) {
        throw 'News items did not survive a reload from the saved file.'
    }
    Write-Output 'Configurator smoke check passed: maximized content bounds, per-post confirmation, save/reload, and preservation of separate News posts.'
    $configForm.Close()
    $configForm.Dispose()
}
finally {
    $resolvedTarget = [IO.Path]::GetFullPath($tempTarget)
    if (-not $resolvedTarget.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove smoke directory outside the project: $resolvedTarget"
    }
    if (Test-Path -LiteralPath $resolvedTarget) {
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}
