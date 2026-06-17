cask "parallax-capture" do
  version "1.1.0"
  sha256 "REPLACE_WITH_RELEASE_SHA256"

  url "https://github.com/Master0fFate/parallax-capture/releases/download/v#{version}/ParallaxCapture-v#{version}-osx-arm64.dmg"
  name "Parallax Capture"
  desc "Screenshot and screen recording tool"
  homepage "https://github.com/Master0fFate/parallax-capture"

  app "Parallax Capture.app"

  zap trash: [
    "~/Library/Application Support/Parallax Capture",
    "~/Library/Logs/Parallax Capture",
    "~/Library/Preferences/com.master0ffate.parallax-capture.plist",
  ]
end
