import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import { unzipSync, strFromU8 } from 'fflate';
import ts from 'typescript';
import fengari from 'fengari';

// Execute the exact packaged backend and frontend handshake. Steam, game files
// and the user's configuration are never accessed by this harness.
const files = unzipSync(fs.readFileSync(process.argv[2] || new URL('../.bundles/library-details.zip', import.meta.url)));
const source = path => strFromU8(files[`NativeGameLinkForSteam/${path}`]);
const { lua, lauxlib, lualib, to_luastring: bytes, to_jsstring: string } = fengari;
const state = lauxlib.luaL_newstate();
lualib.luaL_openlibs(state);
function read(index) {
  index = lua.lua_absindex(state, index);
  switch (lua.lua_type(state, index)) {
    case lua.LUA_TNIL: return null;
    case lua.LUA_TBOOLEAN: return lua.lua_toboolean(state, index);
    case lua.LUA_TNUMBER: return lua.lua_tonumber(state, index);
    case lua.LUA_TSTRING: return string(lua.lua_tostring(state, index));
    case lua.LUA_TTABLE: {
      const result = {};
      lua.lua_pushnil(state);
      while (lua.lua_next(state, index)) { result[read(-2)] = read(-1); lua.lua_pop(state, 1); }
      return result;
    }
    default: throw Error('Unsupported fixture value');
  }
}
function push(value) {
  if (value === null || value === undefined) lua.lua_pushnil(state);
  else if (typeof value === 'string') lua.lua_pushstring(state, bytes(value));
  else if (typeof value === 'number') lua.lua_pushnumber(state, value);
  else if (typeof value === 'boolean') lua.lua_pushboolean(state, value);
  else {
    lua.lua_newtable(state);
    for (const [key, item] of Object.entries(value)) { push(item); lua.lua_setfield(state, -2, bytes(key)); }
  }
}
function execute(code) {
  if (lauxlib.luaL_dostring(state, bytes(code)) !== lua.LUA_OK) throw Error(read(-1));
}
function jsonFunction(name, callback) {
  lua.lua_pushcfunction(state, () => {
    try { push(callback(read(1))); return 1; }
    catch { return lauxlib.luaL_error(state, bytes('Invalid JSON')); }
  });
  lua.lua_setfield(state, -2, bytes(name));
}
lua.lua_newtable(state);
jsonFunction('decode', JSON.parse);
jsonFunction('encode', JSON.stringify);
lua.lua_setglobal(state, bytes('fixture_json'));
push(source('backend/lib/util.lua')); lua.lua_setglobal(state, bytes('util_source'));
push(source('backend/lib/mappings.lua')); lua.lua_setglobal(state, bytes('mappings_source'));
execute(`
  fixture_state = {}
  local function prohibited() error('Unexpected filesystem/process operation') end
  io.open, io.popen, os.rename, os.remove, os.execute = prohibited, prohibited, prohibited, prohibited, prohibited
  package.preload.logger = function() return setmetatable({}, { __index = function() return function() end end }) end
  package.preload.millennium = function() return {ready = function() end} end
  package.preload.http = function() return {} end
  package.preload.json = function() return fixture_json end
  package.preload.fs = function() return {
    parent_path = function(path) return path:match('^(.*)/[^/]+$') or '' end,
    join = function(...) return table.concat({...}, '/') end,
  } end
  MILLENNIUM_PLUGIN_SECRET_BACKEND_ABSOLUTE = 'C:/fixture/backend/main.lua'
  loadfile = function(path)
    if path:match('/util.lua$') then return assert(load(util_source)) end
    if path:match('/mappings.lua$') then return assert(load(mappings_source)) end
    if path:match('/config.lua$') then return function() return function() return {
      state_path = function(name) return name end,
      get_config_path = function() return 'mappings.json' end,
      read_json = function(path, fallback) return fixture_state[path] or fallback end,
      write_json_atomic = function(path, value) fixture_state[path] = value; return true end,
    } end end end
    error('Unexpected backend module: '..path)
  end
`);
execute(source('backend/main.lua'));
lua.lua_pop(state, 1); // lifecycle export
function backend(name, request) {
  lua.lua_getglobal(state, bytes(name));
  push(request);
  if (lua.lua_pcall(state, 1, 1, 0) !== lua.LUA_OK) throw Error(read(-1));
  const result = read(-1); lua.lua_pop(state, 1); return result;
}

// Find the production handshake by its diagnostic contract, not minified names.
const ast = ts.createSourceFile('index.js', source('.millennium/Dist/index.js'), ts.ScriptTarget.Latest, true, ts.ScriptKind.JS);
const functions = [];
function visit(node) {
  if (ts.isFunctionDeclaration(node) && node.body?.getText(ast).includes('steam_appid_file_unconfirmed')) functions.push(node);
  ts.forEachChild(node, visit);
}
visit(ast);
assert.equal(functions.length, 1, 'Packaged handshake must have one implementation');
const handshake = functions[0];
const calls = [], conditionals = [];
function collect(node) {
  if (ts.isCallExpression(node) && ts.isIdentifier(node.expression)) calls.push(node.expression.text);
  if (ts.isConditionalExpression(node) && node.condition.getText(ast).includes('"neutralize"')) conditionals.push(node);
  ts.forEachChild(node, collect);
}
collect(handshake);
assert.equal(conditionals.length, 1, 'Backend operation routing must be identifiable');
const routing = conditionals[0];
const context = { setTimeout, clearTimeout };
for (const name of calls) if (!['setTimeout', 'clearTimeout', 'Error'].includes(name)) context[name] = () => {};
context[routing.whenTrue.expression.getText(ast)] = async request => backend('neutralize_steam_appid_file', request.request_json);
context[routing.whenFalse.expression.getText(ast)] = async request => backend('restore_steam_appid_file', request.request_json);
const protect = vm.runInNewContext(`(${handshake.getText(ast)})`, context);
let checks = 0;
function check(value, message) { assert.ok(value, message); checks++; console.log(`PASS: ${message}`); }
try {
  for (const exe_path of [
    'C:\\Users\\Wesley\\Documents\\UWPHook-2.16.0\\UWPHook.exe',
    '"D:/Jogos/Meus atalhos/UWPHOOK.EXE"',
    '\\\\server\\share\\UWPHook.exe',
  ]) for (const operation of ['neutralize', 'restore']) {
    const request = { exe_path, start_dir: 'D:\\XboxGames\\Content', expires_at_ms: Date.now() + 3500 };
    const result = JSON.parse(backend(`${operation}_steam_appid_file`, JSON.stringify(request)));
    check(result.ok === true && !result.skipped && !result.modified && !result.restored,
      `${operation}: UWPHook requires no licensing-file changes`);
    check(await protect(operation, request), `${operation}: packaged frontend accepts the backend confirmation`);
  }
  for (const exe_path of ['', 'UWPHook.exe', 'C:\\Games\\game.exe', 'C:\\Games\\UWPHook.exe --launch', 'C:\\UWPHook.exe\\other.exe']) {
    check(!await protect('neutralize', {exe_path, start_dir:'C:\\Games'}), 'Other or ambiguous targets cannot enable file maintenance');
  }
  check(JSON.parse(backend('neutralize_steam_appid_file', 'invalid json')).ok === false, 'Malformed IPC requests are rejected');
  check(backend('suppress_admin_prompt', '{}') === true, 'Prompt function preserves its boolean no-op contract');
  for (const name of ['sync_steam_account_achievements', 'start_steam_card_farming'])
    check(JSON.parse(backend(name, '{}')).ok === false, `${name} remains disabled`);

  // Follow the real successful protection reply with the packaged mapping commit.
  check(await protect('neutralize', {exe_path: 'C:\\UWPHook\\UWPHook.exe', start_dir:'C:\\UWPHook'}), 'Link transaction passes the formerly failing step');
  const result = JSON.parse(backend('update_mappings', JSON.stringify({set:{'shortcut:2838666077':'3595270'},remove:[]})));
  check(result.ok && JSON.parse(backend('get_all_mappings', '{}'))['shortcut:2838666077'] === '3595270', 'Packaged backend commits the MWIII mapping after protection');
  execute("assert(fixture_state['mappings.backup.json']['shortcut:2838666077'] == '3595270')");
  check(true, 'Committed mapping has a persistent backup');

  // Validate the C# importer's actual output against the compiled extension's
  // shortcutAlreadyLinked predicate (which suppresses the native review modal).
  const declarations = [];
  function findDeclarations(node) { if (ts.isFunctionDeclaration(node)) declarations.push(node); ts.forEachChild(node, findDeclarations); }
  findDeclarations(ast);
  const keyFunctions = declarations.filter(node => node.parameters.length === 1
    && node.body?.statements.length === 1 && node.body.getText(ast).includes('return"shortcut:"+String('));
  assert.equal(keyFunctions.length, 1, 'The committed shortcut namespace must have one implementation');
  const keyFunction = keyFunctions[0];
  const linkedFunctions = declarations.filter(node => node.parameters.length === 1 && node.body?.statements.length === 1
    && ts.isReturnStatement(node.body.statements[0]) && ts.isBinaryExpression(node.body.statements[0].expression)
    && node.body.statements[0].expression.operatorToken.kind === ts.SyntaxKind.AmpersandAmpersandToken && node.body.getText(ast).length < 220
    && node.body.getText(ast).includes('.test(String(') && node.body.getText(ast).includes(keyFunction.name.text + '('));
  assert.equal(linkedFunctions.length, 1, 'Packaged linked-shortcut predicate must be identifiable');
  const linked = linkedFunctions[0], returned = linked.body.statements[0].expression;
  const mappingVariable = returned.right.arguments[0].arguments[0].left.expression.getText(ast);
  const verifiedFunction = returned.left.expression.getText(ast);
  const fixture = JSON.parse(fs.readFileSync(new URL('../.bundles/import-contract.json', import.meta.url), 'utf8'));
  const registryContext = { [verifiedFunction]: () => true,
    [mappingVariable]: fixture, [keyFunction.name.text]: vm.runInNewContext(`(${keyFunction.getText(ast)})`) };
  const alreadyLinked = vm.runInNewContext(`(${linked.getText(ast)})`, registryContext);
  check(alreadyLinked(2838666077), 'One-action C# import is already linked in the packaged Steam frontend');
  check(Object.keys(fixture).filter(key => key.startsWith('shortcut:')).every(key => alreadyLinked(Number(key.slice(9)))),
    'Fresh-profile and existing-profile imports both bypass manual linking');
  registryContext[mappingVariable] = {'2838666077':'3595270'};
  check(!alreadyLinked(2838666077), 'Legacy bare-number mappings reproduce the extra linking step');
  registryContext[mappingVariable] = fixture;
  check(alreadyLinked(2838666077), 'Imported mappings bypass the native-add linking review predicate');
  console.log(`${checks} companion runtime checks passed. No live Steam files were accessed.`);
} finally { lua.lua_close(state); }
