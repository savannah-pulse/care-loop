import tseslint from 'typescript-eslint'
import jsdoc from 'eslint-plugin-jsdoc'
import tsdoc from 'eslint-plugin-tsdoc'

export default [
  { ignores: ['dist/**', 'node_modules/**', 'src/api/schema.d.ts'] },
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true } } },
    plugins: { jsdoc, tsdoc },
    settings: { jsdoc: { mode: 'typescript' } },
    rules: {
      'tsdoc/syntax': 'error',
      'jsdoc/require-jsdoc': ['error', {
        publicOnly: true,
        require: { FunctionDeclaration: true, ClassDeclaration: true },
        contexts: ['TSInterfaceDeclaration', 'TSTypeAliasDeclaration', 'ExportNamedDeclaration > VariableDeclaration'],
      }],
    },
  },
]
