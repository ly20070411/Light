const http = require('http');

// Test the project.analyze tool after the threading fix
const testData = JSON.stringify({
  Id: 'final-test',
  Type: 'request', 
  Method: 'project.analyze',
  Params: {
    includeAssets: false,
    includePackages: true,
    includeScenes: true,
    includeSettings: true
  }
});

const options = {
  hostname: 'localhost',
  port: 8090,
  path: '/',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': testData.length
  }
};

console.log('🔧 Testing Unity MCP after threading fix...');

const req = http.request(options, (res) => {
  let response = '';
  res.on('data', (chunk) => response += chunk);
  res.on('end', () => {
    console.log('\n📋 Unity Response:');
    try {
      const parsed = JSON.parse(response);
      if (parsed.Error) {
        console.log('❌ Error:', parsed.Error.Message);
      } else if (parsed.Result) {
        console.log('✅ SUCCESS! Unity tools working properly');
        console.log('📊 Project Info:');
        if (parsed.Result.project) {
          console.log(`   - Project: ${parsed.Result.project.projectName}`);
          console.log(`   - Unity Version: ${parsed.Result.project.unityVersion}`);
          console.log(`   - Platform: ${parsed.Result.project.platform}`);
        }
        if (parsed.Result.scenes) {
          console.log(`   - Total Scenes: ${parsed.Result.scenes.totalScenes}`);
          console.log(`   - Active Scene: ${parsed.Result.scenes.activeScene}`);
        }
        console.log('\n🎉 MCP Integration is working correctly!');
      } else {
        console.log('⚠️  Unexpected response format');
      }
    } catch (e) {
      console.log('❌ Could not parse response:', e.message);
      console.log('Raw response:', response);
    }
  });
});

req.on('error', (e) => {
  console.error('❌ Request failed:', e.message);
  console.log('Make sure Unity is running with the MCP server started');
});

req.write(testData);
req.end(); 