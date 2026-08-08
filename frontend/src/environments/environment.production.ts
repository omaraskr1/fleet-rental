export const environment = {
  production: true,

  // Replaced at deploy time with the real API origin.
  apiBaseUrl: 'https://api.fleetrental.example.com/api',

  // Set to the real customer's company code at deploy time to auto-select it
  // and hide the multi-company picker. Leave '' once more than one company is
  // onboarded, to bring the picker back.
  defaultCompanyCode: '',
};
