'use strict';
const fs=require('fs');
const path=require('path');
const rules=require('../src/text-input-rules');
const vectors=JSON.parse(fs.readFileSync(path.resolve(__dirname,'..','..','..','..','shared','text-input-rule-vectors.json'),'utf8'));
for(const vector of vectors){const actual=rules.allowed(vector.text,vector.rules);if(actual!==vector.allowed)throw new Error(`${vector.name}: allowed=${actual}, expected=${vector.allowed}`);if(Object.prototype.hasOwnProperty.call(vector,'complete')){const complete=rules.complete(vector.text,vector.rules);if(complete!==vector.complete)throw new Error(`${vector.name}: complete=${complete}, expected=${vector.complete}`);}}
console.log(`INPUT_RULE_PARITY_SMOKE_OK vectors=${vectors.length}`);
